# FreightHub

Projeto pessoal de portfólio: uma API de cálculo de frete para checkout (modelada em torno do formato "Frete por API" da Yampi), agregando cotações de múltiplas transportadoras (Correios, Loggi, Fretops) e aplicando um motor de regras de negócio (frete grátis, preço fixo por região, por data, por dia da semana etc). Inclui um portal administrativo para gerenciar credenciais, transportadoras, regras e histórico de cotações.

> Este é um projeto de demonstração: nomes, credenciais e CEPs de exemplo neste README são fictícios. O código foi extraído e genericado a partir de um projeto real para servir de amostra de arquitetura.

## Rodando em 2 minutos (dados de exemplo)

Sem configurar nenhuma transportadora real, o projeto já sobe funcional: o motor de regras roda com algumas regras de exemplo (seedadas automaticamente no primeiro start) e, quando nenhuma regra decide o frete, a API responde com um frete de fallback fixo configurável — não é necessário contrato com Correios/Loggi/Fretops para ver o sistema funcionando.

```bash
cp .env.example .env
docker compose up -d postgres

cd Api && dotnet run --project Api   # http://localhost:5066
```

```bash
cd Web && npm install && npm run dev  # http://localhost:3000
```

No primeiro start (banco vazio), a API cria automaticamente:
- um usuário admin, com as credenciais de `Auth:InitialAdmin` (por padrão, em `appsettings.Development.json`: `admin@example.com` / `Admin123!` — troque antes de expor isso publicamente);
- algumas regras de frete de exemplo (frete grátis acima de R$ 300, frete fixo por valor mínimo, frete grátis às domingos, frete grátis por faixa de CEP);
- um histórico de cotações de exemplo, para a tela de Histórico não começar vazia.

Faça login no portal com o usuário acima e explore: Regras de frete, Simulador, Integrações, Usuários e Histórico.

## Stack

| Camada | Tecnologia |
|---|---|
| Backend | ASP.NET Core (.NET 10), Entity Framework Core, Polly (resiliência HTTP) |
| Banco | PostgreSQL |
| Frontend (portal) | Vue 3 + Vite + Vuetify + TypeScript |
| Auth | JWT (usuários do portal) |

Arquitetura do backend em camadas (`Api/Api/`):

```
Domain/              # Entidades e abstrações (regras, transportadoras, integrações)
Application/         # Casos de uso (quote, simulador, regras, usuários)
Infrastructure/      # Integrações externas (Correios/Loggi/Fretops), persistência (EF Core), segurança
Endpoints/           # Handlers HTTP (Minimal APIs)
```

## Transportadoras integradas

Todas implementam a interface comum `ICarrierProvider` ([Domain/Carriers/ICarrierProvider.cs](Api/Api/Domain/Carriers/ICarrierProvider.cs)) e são consultadas em paralelo pelo `CarrierQuoteAggregator`, com isolamento de falhas (uma transportadora lenta/fora do ar não derruba as outras) e pipeline de resiliência (timeout, retry, circuit breaker) próprio por transportadora. Por padrão, todas vêm **desabilitadas** (`Carriers:*:Enabled = false`) — habilite pelo portal (Integrações) só se tiver credenciais reais para testar.

| Transportadora | Autenticação | Particularidade |
|---|---|---|
| **Correios** | Login/código de acesso/contrato (Bearer) | `service` devolvido no formato `CONTRATO_{código}` (ex.: `CONTRATO_03220`) |
| **Loggi** | OAuth2 (client id/secret) | `service` no formato genérico `SERVICO_TRANSPORTADORA` (ex.: `ECONOMICO_LOGGI`) |
| **Fretops** | API key estática (header `X-Fretops-Api-Key`) | `quote_id` devolvido precisa ser o `checkout_ref` atribuído pela própria Fretops à cotação (não pode ser inventado); `name`/`service` são devolvidos exatamente como recebidos |

O liga/desliga de cada transportadora em runtime (sem redeploy) fica na tabela `carrier_settings`, gerenciável via portal ou `PUT /api/v1/admin/carriers/{carrier}`.

## Configuração

Principais variáveis de ambiente (ver [.env.example](.env.example)):

- `Credentials__EncryptionKey` — chave AES-256 (Base64) usada para cifrar credenciais de integração no banco. Gerar com `openssl rand -base64 32`.
- `Jwt__SigningKey` — chave de assinatura do JWT do portal. Gerar com `openssl rand -base64 48`.
- `Auth__InitialAdmin__Email` / `Auth__InitialAdmin__Password` — usuário admin criado automaticamente no primeiro start (quando não há nenhum usuário cadastrado).

Rodando com `dotnet run` (ambiente `Development`), essas chaves já vêm preenchidas com valores de desenvolvimento em `appsettings.Development.json` — **gere as suas próprias antes de qualquer deploy real**, os valores do repositório são só para rodar localmente.

Configuração de transportadoras e do webhook fica em `appsettings.json` (seções `Carriers` e `Yampi`) — URLs base, timeouts, e o frete de fallback usado quando nenhuma transportadora responde a tempo ou nenhuma está habilitada.

### Credenciais de integração

Chaves/tokens de cada transportadora **não** ficam em `appsettings.json` — são cadastradas via portal (Integrações) ou `PUT /api/v1/admin/credentials`, cifradas no banco (`integration_credentials`, AES-256-GCM). Cada credencial é identificada por `Integration` + `Environment` + `Key`:

| Integration | Environment | Key | Descrição |
|---|---|---|---|
| `Correios` | Homolog/Production | `Login`, `CodigoAcesso`, `CartaoPostagem`, `Contrato`, `Dr` | |
| `Loggi` | Homolog/Production | `ClientId`, `ClientSecret`, `CompanyId`, `ExternalServiceId` (opcional) | |
| `Fretops` | Homolog/Production | `ApiKey` | Chave enviada pela Fretops por canal separado |
| `Yampi` | Homolog/Production | `SecretKey` | Usada para validar a assinatura HMAC do webhook |

## Principais endpoints da API

| Rota | Descrição |
|---|---|
| `POST /api/v1/shipping/quote` | Webhook público de cotação ("Frete por API") — cota transportadoras + regras, valida assinatura HMAC |
| `POST /api/v1/shipping/simulate` | Simulador (autenticado, usado pelo portal) — mesma lógica de cotação, em modo dry-run |
| `POST /api/v1/auth/login` | Login do portal |
| `/api/v1/admin/carriers` | Liga/desliga transportadoras em runtime |
| `/api/v1/admin/credentials` | CRUD de credenciais de integração (cifradas) |
| `/api/v1/rules` | CRUD do motor de regras de frete |
| `/api/v1/quote-logs` | Consulta ao histórico de cotações (retenção configurável) |
| `/api/v1/users` | CRUD de usuários do portal |

## Testes

```bash
cd Api
dotnet test
```

## Licença

[MIT](LICENSE)
