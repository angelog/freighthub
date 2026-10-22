<template>
  <div>
    <h1 class="text-h5 mb-4">Painel</h1>

    <v-row>
      <v-col
        v-for="card in cards"
        :key="card.title"
        cols="12"
        md="3"
        sm="6"
      >
        <v-card :to="card.to" variant="tonal">
          <v-card-text class="d-flex align-center">
            <v-icon class="mr-4" :color="card.color" :icon="card.icon" size="40" />

            <div>
              <div class="text-h5">{{ card.value }}</div>
              <div class="text-body-2 text-medium-emphasis">{{ card.title }}</div>
            </div>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>
  </div>
</template>

<script lang="ts" setup>
  import { computed, onMounted, ref } from 'vue'
  import { api } from '@/api/client'

  const rulesActive = ref(0)
  const carriersEnabled = ref(0)
  const usersCount = ref(0)
  const quotesCount = ref(0)

  const cards = computed(() => [
    { title: 'Regras ativas', value: rulesActive.value, icon: 'mdi-ruler-square', color: 'primary', to: '/rules' },
    { title: 'Transportadoras ligadas', value: carriersEnabled.value, icon: 'mdi-truck', color: 'secondary', to: '/integrations' },
    { title: 'Usuários', value: usersCount.value, icon: 'mdi-account-group', color: 'info', to: '/users' },
    { title: 'Cotações (7 dias)', value: quotesCount.value, icon: 'mdi-history', color: 'warning', to: '/history' },
  ])

  onMounted(async () => {
    const [rules, carriers, users, logs] = await Promise.all([
      api.get('/rules'),
      api.get('/admin/carriers'),
      api.get('/users'),
      api.get('/quote-logs?limit=500'),
    ])
    rulesActive.value = rules.data.filter((r: any) => r.isActive).length
    carriersEnabled.value = carriers.data.filter((c: any) => c.enabled).length
    usersCount.value = users.data.length
    quotesCount.value = logs.data.length
  })
</script>
