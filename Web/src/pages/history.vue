<template>
  <div>
    <h1 class="text-h5 mb-4">Histórico de cotações</h1>
    <p class="text-body-2 text-medium-emphasis mb-4">Cotações recebidas nos últimos 7 dias (webhook da Yampi).</p>

    <v-card>
      <v-data-table
        :headers="headers"
        :items="logs"
        :loading="loading"
        :sort-by="[{ key: 'createdAt', order: 'desc' }]"
      >
        <template #item.createdAt="{ value }">{{ new Date(value).toLocaleString('pt-BR') }}</template>
        <template #item.cartAmount="{ value }">{{ brl(value) }}</template>

        <template #item.signatureValid="{ value }">
          <v-icon :color="value ? 'success' : 'error'" :icon="value ? 'mdi-check-circle' : 'mdi-alert-circle'" />
        </template>

        <template #item.ruleDecided="{ value }">
          <v-chip size="small">{{ value ? 'Regra' : 'Transportadora' }}</v-chip>
        </template>

        <template #item.usedFallback="{ value }">
          <v-icon v-if="value" color="warning" icon="mdi-alert" title="Usou fallback" />
        </template>
      </v-data-table>
    </v-card>
  </div>
</template>

<script lang="ts" setup>
  import { ref } from 'vue'
  import { api } from '@/api/client'

  const headers = [
    { title: 'Quando', key: 'createdAt' },
    { title: 'CEP', key: 'destinationZipCode' },
    { title: 'Carrinho', key: 'cartAmount' },
    { title: 'Duração (ms)', key: 'durationMs' },
    { title: 'Decidido por', key: 'ruleDecided' },
    { title: 'Cotações', key: 'carrierQuoteCount' },
    { title: 'Fallback', key: 'usedFallback' },
    { title: 'Assinatura', key: 'signatureValid' },
  ]

  const logs = ref<any[]>([])
  const loading = ref(false)

  const brl = (v: number) => v.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })

  async function load () {
    loading.value = true
    try {
      logs.value = (await api.get('/quote-logs?limit=200')).data
    } finally {
      loading.value = false
    }
  }

  load()
</script>
