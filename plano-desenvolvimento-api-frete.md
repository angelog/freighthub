# Plano de desenvolvimento — API de cálculo de frete (Yampi + Correios + Loggi)

## 1. Contexto e decisão de stack

**Contexto informado:** loja única (uso interno), backend em .NET, portal admin em **Vue 3 + TypeScript + Pinia + Vuetify** (ajustado a pedido), deploy em VPS próprio via Coolify. **Sem cache (Redis) nesta primeira etapa.**

**Stack recomendada:**

| Camada | Tecnologia | Papel |
|---|---|---|
| Backend | ASP.NET Core 9 (Web API) | Webhook da Yampi, motor de regras, integração com transportadoras, API do portal admin |
| Frontend | Vue 3 (TypeScript, Composition API) + Pinia + Vuetify | Portal admin — CRUD de regras, simulador de frete |
| Banco | PostgreSQL | Regras, histórico de cotações (retenção de 7 dias), usuários do portal |
| Cache | *Nenhum nesta fase* | Chamadas a Correios/Loggi feitas diretamente a cada requisição — ver seção 5 |
| Deploy | Docker + docker-compose no Coolify | 3 serviços: api, web, postgres |

### Por que separar backend (.NET) do frontend (Vue)

1. **O motor de regras é o produto de verdade.** Ele decide, com dinheiro real, se um cliente paga frete ou não. Isso pede tipagem forte, testes unitários fáceis (xUnit) e um design de domínio explícito — coisas em que C#/.NET é particularmente confortável.
2. **O webhook é síncrono e crítico.** A Yampi chama sua API no meio do checkout do cliente final e espera resposta rápida. Chamar Correios e Loggi por baixo dos panos exige timeout, retry com backoff e circuit breaker — o Polly (biblioteca padrão do ecossistema .NET) resolve isso de forma declarativa, sem você reinventar a roda em JavaScript.
3. **Separação de responsabilidade e de risco.** O endpoint público que a Yampi chama fica isolado da autenticação/UI do portal admin. Um bug na tela de regras não deve conseguir derrubar o cálculo de frete em produção.
4. **Publicar não é argumento a favor do monólito aqui.** O Coolify builda cada serviço a partir de um `Dockerfile` — dois serviços não são operacionalmente mais complexos que um, só significa dois builds automáticos no mesmo git push.
5. **Vue como camada 100% de apresentação.** Com Pinia guardando o estado (sessão, regras carregadas, resultado do simulador) e Vuetify cobrindo os componentes de formulário/tabela prontos, o portal fica enxuto — toda a lógica de negócio pesada continua isolada na API .NET, sem duplicar regra nenhuma no frontend.

> A ausência de cache nesta primeira fase é uma escolha deliberada de simplicidade — ver seção 5 para o que isso implica em latência/risco de rate limit, e quando reconsiderar adicionar Redis.

---

## 2. Arquitetura geral

```
Yampi (checkout)  ──POST /shipping/quote──┐
                                           ▼
Portal admin (Vue 3) ──API autenticada──▶ .NET API
                                           │  ├─ Webhook layer (valida assinatura, monta contexto)
                                           │  ├─ Domain (motor de regras)
                                           │  └─ Infrastructure (clients Correios/Loggi, EF Core)
                                           │
                              ┌────────────┼────────────┐
                              ▼            ▼             ▼
                        Correios API   Loggi API      Postgres
```

A API .NET é organizada em 3 camadas (Clean Architecture simplificada):

- **Api** — controllers/endpoints, middlewares de autenticação e validação de assinatura, DTOs de entrada/saída.
- **Domain** — entidades (`ShippingRule`, `Quote`, `Cart`), o motor de regras (`IShippingRule` + `RuleEngine`), sem dependência de nada externo (fácil de testar isoladamente).
- **Infrastructure** — `ICorreiosClient`, `ILoggiClient` (com Polly), `EF Core` + migrations, job de expurgo de logs (ver seção 6.1).

---

## 3. Especificação do endpoint (webhook da Yampi)

A Yampi funciona no modo **"Frete por API"**: você cadastra uma URL no painel dela, junto de até 5 headers de autenticação. A cada cálculo de frete no checkout, a Yampi faz um `POST` para essa URL e espera de volta uma lista de opções de frete. Eles fornecem uma **secret key** para você validar que a requisição realmente veio deles.

**Endpoint:** `POST /api/v1/shipping/quote`

**Responsabilidades do endpoint:**
1. Validar a assinatura/secret key recebida no header (rejeitar com 401 se inválida).
2. Extrair CEP de destino, itens do carrinho (peso, dimensões, valor) e a data/hora da requisição.
3. Rodar o **motor de regras** (regras internas, sem custo de rede) primeiro — se uma regra decidir o frete (ex: grátis por região, fixo por valor mínimo), responder direto.
4. Se a decisão depender de cotação real de transportadora, consultar Correios/Loggi diretamente (sem cache nesta fase — ver seção 5) e aplicar as regras de ajuste sobre o resultado.
5. Responder em JSON no formato esperado pela Yampi (nome do serviço, valor, prazo em dias).
6. Nunca deixar a requisição "pendurada": timeout total do endpoint configurado (ex: 4–5s) com fallback (frete fixo configurável) se as transportadoras não responderem a tempo.

**Registrar internamente cada requisição/resposta** (log estruturado) — essencial para debugar "por que esse cliente recebeu esse valor de frete" depois. Esses registros seguem a política de retenção de 7 dias descrita na seção 6.1.

---

## 4. Motor de regras

### 4.1 Modelo

Cada regra implementa uma interface comum:

```csharp
public interface IShippingRule
{
    int Priority { get; }
    bool AppliesTo(ShippingContext context);
    RuleResult Apply(ShippingContext context, RuleResult current);
}
```

O `RuleEngine` roda as regras em ordem de prioridade (regras pontuais > regras de exceção por data > regras fixas gerais), permitindo que uma regra sobrescreva ou combine com o resultado da anterior. Isso evita a armadilha clássica de "if aninhado" ilegível quando você tem 8+ regras concorrentes.

### 4.2 Regras fixas (mapeamento para o modelo)

| Regra descrita | Implementação sugerida |
|---|---|
| Valor mínimo → frete fixo | `MinimumOrderFixedShippingRule` — compara valor do carrinho, retorna valor fixo configurável |
| Acima de valor → frete grátis | `FreeShippingAboveValueRule` — valor mínimo configurável no portal |
| Dias de semana / fim de semana com valor diferente | `WeekdayPricingRule` — avalia `DayOfWeek` do momento da cotação |
| Domingo frete grátis | Caso específico do `WeekdayPricingRule`, prioridade alta |
| Frete grátis por região (CEP) | `RegionFreeShippingRule` — faixas de CEP cadastradas (início/fim), não CEP exato |
| "Regra para escolher o frete grátis" (quando mais de uma opção é elegível) | Critério de desempate configurável: mais barato, mais rápido, ou transportadora preferida |

### 4.3 Regras pontuais

| Regra | Implementação |
|---|---|
| Dia específico | `SpecificDateRule` — data exata, ex: Black Friday |
| Range de dias | `DateRangeRule` — início/fim, ex: campanha de uma semana |

Ambas com prioridade mais alta que as regras fixas gerais (uma promoção pontual deve sobrepor a regra padrão).

### 4.4 Área de consulta interna (simulador)

Endpoint separado (autenticado, só para o portal admin): recebe os mesmos parâmetros do webhook da Yampi, roda o motor de regras em **modo dry-run**, e retorna não só o resultado final mas **qual(is) regra(s) decidiram o valor** — isso é o que torna o "teste de regras" do portal realmente útil (sem isso, debugar por que uma regra não disparou é adivinhação).

---

## 5. Integração com transportadoras

### Correios (API Prazo)

- **Restrita a clientes de contrato** — precisa ter o serviço **38210 – API Prazo** vinculado ao contrato/cartão de postagem. Sem isso, a API recusa a chamada.
- Autenticação **Bearer Token**, obtido via Correios Web Service (CWS) ou chave de sub-delegação de acesso.
- **A API Prazo só devolve prazo de entrega, não preço.** Para o valor do frete você provavelmente vai precisar também da **API Preço** dos Correios (contrato separado) — confirme isso com o time comercial dos Correios antes de assumir que uma cobre a outra.
- Ambientes de homologação e produção com URLs diferentes; teste tudo em homologação primeiro.
- Rate limit: 150 requisições/segundo por usuário/IP — folgado para uma loja única, não é uma preocupação real aqui.

### Loggi (Quotation API)

- Endpoint `POST /v1/companies/{company_id}/quotations`, autenticação OAuth2/Bearer.
- Retorna preço e prazo já combinados por pacote, com base em `shipFrom`, `shipTo`, `packages` e tipo de coleta.
- Funciona mesmo com poucos campos preenchidos, mas a precisão aumenta preenchendo tudo (peso, dimensões).

### Padrão de implementação (ambas)

- Interface comum `ICarrierProvider` (`GetQuoteAsync(request) -> CarrierQuote[]`), uma implementação por transportadora.
- **Polly** aplicado no `HttpClient` de cada client: timeout curto (ex: 2s), retry com backoff exponencial (2 tentativas), circuit breaker (se a transportadora falhar repetidamente, para de tentar por um tempo e usa fallback).
- **Sem cache nesta primeira fase**, por decisão explícita — cada requisição do webhook chama Correios/Loggi diretamente. Isso é aceitável para uma loja única, mas vale reavaliar se: (a) o volume de checkouts simultâneos crescer, (b) o rate limit de alguma transportadora começar a ser um risco real, ou (c) a latência do checkout ficar perceptível para o cliente. Se algum desses acontecer, adicionar Redis com TTL curto (15–30 min) por `{transportadora}:{cepOrigem}:{cepDestino}:{faixaDePeso}` é a extensão natural, sem precisar redesenhar nada — a interface `ICarrierProvider` já isola essa decisão.
- Se todas as transportadoras falharem, o webhook deve responder com um frete de fallback fixo (configurável no portal) em vez de retornar erro para o checkout da Yampi.

---

## 6. Portal administrativo

- **Stack**: Vue 3 (Composition API + `<script setup>`) em TypeScript, **Pinia** para estado (sessão do usuário, regras carregadas, resultado do simulador), **Vuetify** para os componentes de UI (tabelas, formulários, data pickers) — evita reinventar componente visual e mantém consistência visual sem esforço extra.
- **Autenticação**: login com usuário/senha, JWT emitido pela API .NET; um `authStore` (Pinia) guarda o token/sessão, e um *navigation guard* do Vue Router protege as rotas do portal.
- **CRUD de regras**: criar/editar/desativar cada tipo de regra descrito na seção 4, com prioridade ajustável — tabela Vuetify (`v-data-table`) + formulário (`v-form`) por tipo de regra.
- **Simulador / consulta interna**: formulário com CEP, valor do carrinho, peso, data simulada → mostra o resultado e qual regra decidiu.
- **Histórico de cotações**: requisições recebidas via webhook nos últimos 7 dias, com o resultado retornado (auditoria) — ver política de retenção abaixo.
- **Painel de saúde das integrações**: status das últimas chamadas a Correios/Loggi (sucesso, timeout, erro) — ajuda a perceber rapidamente se uma transportadora está fora do ar.

### 6.1 Retenção dos registros de consulta

Por decisão do projeto, o histórico de cotações (webhook + simulador) guarda **apenas os últimos 7 dias** — não é um log permanente/auditoria de longo prazo.

Implementação sugerida no backend .NET:
- Um `IHostedService` (background service) rodando uma vez por dia (ex: de madrugada), que executa `DELETE FROM shipping_quote_logs WHERE created_at < now() - interval '7 days'`.
- Alternativa mais simples de operar: um `pg_cron` no próprio Postgres agendando a mesma limpeza diretamente no banco, sem depender do processo da API estar de pé no horário certo.
- Qualquer uma das duas opções deve ser coberta por teste (ver seção 10) para garantir que a janela de 7 dias é respeitada de fato, e não é só um comentário no código.

> Se no futuro precisar de histórico mais longo para fins de auditoria/negócio, isso vira uma configuração (dias de retenção) em vez de um valor fixo — mas não é necessário implementar essa flexibilidade agora.

---

## 7. Estrutura de repositório sugerida

```
/api            → ASP.NET Core (Api / Domain / Infrastructure / Tests)
/web            → Vue 3 + TypeScript + Pinia + Vuetify (portal admin)
/docker-compose.yml
/docker-compose.staging.yml
```

Monorepo único facilita manter API e portal sincronizados durante o desenvolvimento inicial (loja única, time pequeno). Cada pasta tem seu próprio `Dockerfile`; o Coolify aponta para o repositório e builda os dois serviços a partir dele.

---

## 8. Roadmap por fases

| Fase | Entrega | Observação |
|---|---|---|
| **0 — Setup** | Repositório, docker-compose local, projeto .NET (Api/Domain/Infra) e Vue 3 (Vuetify) scaffolded, ambientes staging/produção no Coolify | Antes de qualquer regra, garanta que "hello world" builda e sobe pelas duas stacks |
| **1 — Motor de regras** | Domain + regras fixas e pontuais implementadas e testadas (xUnit), sem transportadora real ainda (mock) | Prioridade máxima — é o que gera valor mesmo antes de integrar Correios/Loggi |
| **2 — Integração de transportadoras** | `ICarrierProvider` + Correios + Loggi reais, Polly (sem cache nesta fase) | Depende de contrato ativo com Correios (serviço 38210) e conta na Loggi (`company_id`) — resolver isso em paralelo à Fase 1 |
| **3 — Webhook Yampi** | Endpoint público, validação de assinatura, resposta no formato esperado, fallback configurável, logging + job de expurgo (retenção de 7 dias) | Testar em uma loja de homologação da Yampi antes de apontar produção |
| **4 — Portal admin** | Login (Pinia + guard de rotas), CRUD de regras, simulador com explicação da regra vencedora, histórico (7 dias) | Consumindo a Admin API do backend, componentes Vuetify |
| **5 — Observabilidade e produção** | Logs estruturados, painel de saúde das integrações, deploy final, corte para a loja real | Rodar em paralelo com o frete atual por um período antes de desligar o antigo |

---

## 9. Segurança

- Validar a assinatura/secret key da Yampi em toda requisição do webhook (rejeitar sem validação).
- HTTPS obrigatório ponta a ponta (Coolify já provisiona certificado via Let's Encrypt).
- Rate limiting básico no endpoint público (proteção contra abuso, mesmo sendo baixo volume).
- Segredos (tokens Correios/Loggi, secret da Yampi, connection strings) como variáveis de ambiente no Coolify — nunca commitados no repositório.
- Usuário do banco com permissões mínimas necessárias (sem superuser).
- Autenticação do portal admin separada da autenticação do webhook — nenhuma credencial do portal deve valer para o endpoint público.
- **Retenção de dados**: registros de consulta (webhook + simulador) mantidos por apenas 7 dias e removidos automaticamente (ver seção 6.1) — minimiza a quantidade de dados de CEP/pedido armazenados.

---

## 10. Testes

- **Unitários (xUnit)** para cada regra isoladamente — dado um contexto simulado, a regra retorna o resultado esperado. Essa é a parte não-negociável: bug aqui é dinheiro perdido ou frete grátis indevido.
- **Testes de integração** dos clients Correios/Loggi com mocks de HTTP (não bater na API real nos testes automatizados).
- **Teste do webhook completo** simulando um payload real da Yampi, verificando formato de resposta e comportamento de fallback quando as transportadoras "falham" (mock de timeout).
- **Teste do job de expurgo de logs**: inserir registros com datas simuladas (dentro e fora da janela de 7 dias) e confirmar que só os antigos são removidos.

---

## 11. Próximos passos imediatos

1. Confirmar com os Correios se o contrato atual já tem o serviço **38210 – API Prazo** vinculado, e verificar se será necessário contratar também a **API Preço** separadamente.
2. Criar conta/obter `company_id` e credenciais na Loggi Platform.
3. No painel da Yampi, gerar a configuração de "Frete por API" para obter a URL de callback esperada e a secret key de validação (mesmo antes do backend existir, isso já revela o payload exato que a Yampi envia).
4. Criar o repositório com a estrutura da seção 7 e subir o "hello world" das duas stacks no Coolify — validar o pipeline de deploy antes de escrever qualquer regra de negócio.
5. Modelar as entidades do motor de regras (Fase 1) a partir da lista da seção 4 — esse é o trabalho que mais vale a pena começar primeiro, já que não depende de nenhuma integração externa estar pronta.
