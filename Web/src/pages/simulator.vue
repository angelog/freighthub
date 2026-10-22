<template>
  <div>
    <h1 class="text-h5 mb-4">Simulador de frete</h1>

    <v-row>
      <v-col cols="12" md="5">
        <v-card title="Carrinho">
          <v-card-text>
            <v-text-field v-model="cep" class="mb-2" label="CEP de destino" />

            <v-text-field
              v-model="requestedAt"
              class="mb-1"
              clearable
              hint="Deixe vazio para usar o momento atual"
              label="Data/hora da compra (opcional)"
              persistent-hint
              type="datetime-local"
            />

            <div v-if="weekdayLabel" class="text-caption text-medium-emphasis mb-3">
              Simulando uma compra em <strong>{{ weekdayLabel }}</strong>
            </div>
            <div v-else class="mb-3" />

            <div class="text-body-2 mb-2">Itens</div>

            <div v-for="(it, i) in items" :key="i" class="d-flex ga-2 mb-2">
              <v-text-field
                v-model.number="it.unitPrice"
                density="compact"
                hide-details
                label="Preço un."
                type="number"
              />

              <v-text-field
                v-model.number="it.quantity"
                density="compact"
                hide-details
                label="Qtd"
                type="number"
              />

              <v-text-field
                v-model.number="it.weightKg"
                density="compact"
                hide-details
                label="Peso (kg)"
                type="number"
              />

              <v-btn color="error" icon="mdi-close" variant="text" @click="items.splice(i, 1)" />
            </div>

            <v-btn
              class="mb-4"
              prepend-icon="mdi-plus"
              size="small"
              text="Adicionar item"
              variant="tonal"
              @click="items.push({ unitPrice: 0, quantity: 1, weightKg: 1 })"
            />

            <v-btn
              block
              color="primary"
              :loading="loading"
              text="Simular"
              @click="simulate"
            />
          </v-card-text>
        </v-card>
      </v-col>

      <v-col cols="12" md="7">
        <v-alert v-if="error" type="error">{{ error }}</v-alert>

        <template v-if="result">
          <v-card class="mb-4" title="Opções finais">
            <v-list>
              <v-list-item
                v-for="(o, i) in result.finalOptions"
                :key="i"
                :subtitle="`${o.source} · ${o.estimatedDays ?? '—'} dia(s)`"
                :title="o.name"
              >
                <template #append>
                  <v-chip :color="o.isFree ? 'success' : 'primary'">{{ o.isFree ? 'Grátis' : brl(o.price) }}</v-chip>
                </template>
              </v-list-item>
            </v-list>
          </v-card>

          <v-card class="mb-4" title="Decisão das regras">
            <v-card-text>
              <div v-if="result.ruleDecided">
                Decidida por: <strong>{{ result.decidingRule?.ruleName }}</strong>
              </div>

              <div v-else class="text-medium-emphasis">Nenhuma regra decidiu — foi para as transportadoras.</div>
              <v-chip v-for="r in result.appliedRules" :key="r.ruleId" class="mr-1 mt-2" size="small">{{ r.ruleName }}</v-chip>
            </v-card-text>
          </v-card>

          <v-card title="Cotações das transportadoras">
            <v-list>
              <v-list-item v-for="(q, i) in result.carrierQuotes" :key="i" :subtitle="`${q.carrier} · ${q.estimatedDays} dia(s)`" :title="q.serviceName">
                <template #append>{{ brl(q.price) }}</template>
              </v-list-item>

              <v-list-item v-if="result.carrierQuotes.length === 0" class="text-medium-emphasis" title="Nenhuma cotação retornada" />
            </v-list>
          </v-card>
        </template>
      </v-col>
    </v-row>
  </div>
</template>

<script lang="ts" setup>
  import { computed, ref } from 'vue'
  import { api } from '@/api/client'
  import { apiErrorMessage } from '@/api/errors'
  import { useSnackbar } from '@/stores/snackbar'

  const snackbar = useSnackbar()

  const cep = ref('04543000')
  // Formato do input datetime-local: "YYYY-MM-DDTHH:mm" (vazio = usar agora).
  const requestedAt = ref('')
  const items = ref([{ unitPrice: 150, quantity: 2, weightKg: 1.5 }])
  const result = ref<any>(null)
  const loading = ref(false)
  const error = ref('')

  const brl = (v: number) => v.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })

  // Dia da semana correspondente ao valor escolhido, para confirmação visual.
  const weekdayLabel = computed(() => {
    if (!requestedAt.value) return ''
    const d = new Date(requestedAt.value)
    return Number.isNaN(d.getTime())
      ? ''
      : d.toLocaleDateString('pt-BR', { weekday: 'long', day: '2-digit', month: '2-digit', year: 'numeric' })
  })

  // Converte "YYYY-MM-DDTHH:mm" em ISO com o offset local explícito,
  // preservando a hora "de parede" (evita a conversão para UTC deslocar o dia).
  function toLocalOffsetIso (local: string): string {
    const d = new Date(local)
    const off = -d.getTimezoneOffset() // minutos; ex.: -180 para UTC-3
    const sign = off >= 0 ? '+' : '-'
    const abs = Math.abs(off)
    const pad = (n: number) => String(n).padStart(2, '0')
    return `${local}:00${sign}${pad(Math.floor(abs / 60))}:${pad(abs % 60)}`
  }

  async function simulate () {
    error.value = ''
    result.value = null
    loading.value = true
    try {
      const payload: Record<string, unknown> = { destinationZipCode: cep.value, items: items.value }
      if (requestedAt.value) payload.requestedAt = toLocalOffsetIso(requestedAt.value)
      result.value = (await api.post('/shipping/simulate', payload)).data
    } catch (error_: any) {
      error.value = apiErrorMessage(error_, 'Falha na simulação.')
      snackbar.error(error.value)
    } finally {
      loading.value = false
    }
  }
</script>
