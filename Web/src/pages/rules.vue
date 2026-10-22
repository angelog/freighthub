<template>
  <div>
    <div class="d-flex align-center mb-4">
      <h1 class="text-h5">Regras de frete</h1>
      <v-spacer />
      <v-btn color="primary" prepend-icon="mdi-plus" text="Nova regra" @click="openNew" />
    </div>

    <v-card>
      <v-data-table :headers="headers" :items="rules" :loading="loading" :sort-by="[{ key: 'priority', order: 'desc' }]">
        <template #item.type="{ value }">
          <v-chip size="small">{{ typeLabel(value) }}</v-chip>
        </template>

        <template #item.summary="{ item }">{{ summary(item) }}</template>

        <template #item.isActive="{ item }">
          <v-switch
            color="primary"
            density="compact"
            hide-details
            :model-value="item.isActive"
            @update:model-value="toggle(item)"
          />
        </template>

        <template #item.actions="{ item }">
          <v-btn icon="mdi-pencil" size="small" variant="text" @click="openEdit(item)" />

          <v-btn
            color="error"
            icon="mdi-delete"
            size="small"
            variant="text"
            @click="remove(item)"
          />
        </template>
      </v-data-table>
    </v-card>

    <v-dialog v-model="dialog" max-width="560">
      <v-card :title="editing ? 'Editar regra' : 'Nova regra'">
        <v-card-text>
          <v-alert v-if="error" class="mb-4" density="compact" type="error">{{ error }}</v-alert>

          <v-select
            v-model="form.type"
            class="mb-2"
            :disabled="editing"
            :items="typeItems"
            label="Tipo"
          />

          <v-text-field v-model="form.name" class="mb-2" label="Nome (exibido ao gerenciar)" />

          <v-text-field
            v-model.number="form.priority"
            class="mb-2"
            hint="Maior = avaliada primeiro"
            label="Prioridade"
            type="number"
          />

          <!-- campos por tipo -->
          <template v-if="form.type === 'free_above_value' || form.type === 'min_order_fixed'">
            <v-text-field v-model.number="form.minimumOrderValue" class="mb-2" label="Valor mínimo do carrinho (R$)" type="number" />
          </template>

          <template v-if="form.type === 'min_order_fixed'">
            <v-text-field v-model.number="form.fixedPrice" class="mb-2" label="Valor fixo do frete (R$)" type="number" />
          </template>

          <template v-if="form.type === 'max_order_fixed'">
            <v-text-field v-model.number="form.maximumOrderValue" class="mb-2" label="Valor máximo do carrinho (R$)" type="number" />
            <v-text-field v-model.number="form.fixedPrice" class="mb-2" label="Valor fixo do frete (R$)" type="number" />
          </template>

          <template v-if="form.type === 'weekday'">
            <v-select v-model="form.dayOfWeek" class="mb-2" :items="weekdays" label="Dia da semana" />
          </template>

          <template v-if="form.type === 'specific_date'">
            <v-text-field v-model="form.date" class="mb-2" label="Data" type="date" />
          </template>

          <template v-if="form.type === 'date_range'">
            <v-text-field v-model="form.startDate" class="mb-2" label="Início" type="date" />
            <v-text-field v-model="form.endDate" class="mb-2" label="Fim" type="date" />
          </template>

          <template v-if="['weekday', 'specific_date', 'date_range'].includes(form.type)">
            <v-switch v-model="form.isFree" color="primary" label="Frete grátis" />
            <v-text-field v-if="!form.isFree" v-model.number="form.price" label="Valor do frete (R$)" type="number" />
          </template>

          <template v-if="form.type === 'region_free'">
            <div class="text-body-2 mb-2">Faixas de CEP com frete grátis</div>

            <div v-for="(r, i) in form.cepRanges" :key="i" class="d-flex ga-2 mb-2">
              <v-text-field v-model="r.start" density="compact" hide-details label="CEP início" />
              <v-text-field v-model="r.end" density="compact" hide-details label="CEP fim" />
              <v-btn color="error" icon="mdi-close" variant="text" @click="form.cepRanges.splice(i, 1)" />
            </div>

            <v-btn
              prepend-icon="mdi-plus"
              size="small"
              text="Adicionar faixa"
              variant="tonal"
              @click="form.cepRanges.push({ start: '', end: '' })"
            />
          </template>
        </v-card-text>

        <v-card-actions>
          <v-spacer />
          <v-btn text="Cancelar" @click="dialog = false" />

          <v-btn
            color="primary"
            :loading="saving"
            text="Salvar"
            variant="flat"
            @click="save"
          />
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script lang="ts" setup>
  import { ref } from 'vue'
  import { api } from '@/api/client'
  import { apiErrorMessage } from '@/api/errors'
  import { useConfirm } from '@/stores/confirm'
  import { useSnackbar } from '@/stores/snackbar'

  const snackbar = useSnackbar()
  const confirm = useConfirm()

  interface Rule {
    id: string
    type: string
    name: string
    priority: number
    isActive: boolean
    minimumOrderValue?: number
    maximumOrderValue?: number
    fixedPrice?: number
    isFree?: boolean
    price?: number
    dayOfWeek?: string
    date?: string
    startDate?: string
    endDate?: string
    cepRanges?: { start: string, end: string }[]
  }

  const typeItems = [
    { title: 'Frete grátis acima de valor', value: 'free_above_value' },
    { title: 'Frete fixo acima de valor', value: 'min_order_fixed' },
    { title: 'Frete fixo abaixo de valor', value: 'max_order_fixed' },
    { title: 'Preço por dia da semana', value: 'weekday' },
    { title: 'Frete grátis por faixa de CEP', value: 'region_free' },
    { title: 'Data específica', value: 'specific_date' },
    { title: 'Intervalo de datas', value: 'date_range' },
  ]
  const weekdays = [
    { title: 'Domingo', value: 'Sunday' },
    { title: 'Segunda', value: 'Monday' },
    { title: 'Terça', value: 'Tuesday' },
    { title: 'Quarta', value: 'Wednesday' },
    { title: 'Quinta', value: 'Thursday' },
    { title: 'Sexta', value: 'Friday' },
    { title: 'Sábado', value: 'Saturday' },
  ]
  const headers = [
    { title: 'Tipo', key: 'type' },
    { title: 'Nome', key: 'name' },
    { title: 'Detalhe', key: 'summary', sortable: false },
    { title: 'Prioridade', key: 'priority' },
    { title: 'Ativa', key: 'isActive', sortable: false },
    { title: '', key: 'actions', sortable: false, align: 'end' as const },
  ]

  const rules = ref<Rule[]>([])
  const loading = ref(false)
  const dialog = ref(false)
  const saving = ref(false)
  const editing = ref(false)
  const error = ref('')
  const currentId = ref<string | null>(null)
  const form = ref<any>(blank())

  function blank () {
    return {
      type: 'free_above_value', name: '', priority: 10, isActive: true,
      minimumOrderValue: 200, maximumOrderValue: 100, fixedPrice: 0, isFree: true, price: 0,
      dayOfWeek: 'Sunday', date: '', startDate: '', endDate: '',
      cepRanges: [{ start: '', end: '' }],
    }
  }

  function typeLabel (t: string) {
    return typeItems.find(i => i.value === t)?.title ?? t
  }

  function summary (r: Rule) {
    switch (r.type) {
      case 'free_above_value': { return `Grátis ≥ R$ ${r.minimumOrderValue}`
      }
      case 'min_order_fixed': { return `≥ R$ ${r.minimumOrderValue} → R$ ${r.fixedPrice}`
      }
      case 'max_order_fixed': { return `< R$ ${r.maximumOrderValue} → R$ ${r.fixedPrice}`
      }
      case 'weekday': { return `${weekdays.find(w => w.value === r.dayOfWeek)?.title}: ${r.isFree ? 'grátis' : 'R$ ' + r.price}`
      }
      case 'region_free': { return `${r.cepRanges?.length ?? 0} faixa(s) de CEP`
      }
      case 'specific_date': { return `${r.date}: ${r.isFree ? 'grátis' : 'R$ ' + r.price}`
      }
      case 'date_range': { return `${r.startDate} a ${r.endDate}: ${r.isFree ? 'grátis' : 'R$ ' + r.price}`
      }
      default: { return ''
      }
    }
  }

  async function load () {
    loading.value = true
    try {
      rules.value = (await api.get('/rules')).data
    } finally {
      loading.value = false
    }
  }

  function openNew () {
    editing.value = false
    currentId.value = null
    form.value = blank()
    error.value = ''
    dialog.value = true
  }

  function openEdit (r: Rule) {
    editing.value = true
    currentId.value = r.id
    form.value = { ...blank(), ...r, cepRanges: r.cepRanges?.length ? [...r.cepRanges] : [{ start: '', end: '' }] }
    error.value = ''
    dialog.value = true
  }

  // Monta o payload apenas com os campos do tipo escolhido — evita mandar
  // datas vazias/ids nulos que quebram a desserialização na API.
  function buildPayload () {
    const f = form.value
    const base = { type: f.type, name: f.name, priority: f.priority, isActive: f.isActive }
    switch (f.type) {
      case 'free_above_value': { return { ...base, minimumOrderValue: f.minimumOrderValue }
      }
      case 'min_order_fixed': { return { ...base, minimumOrderValue: f.minimumOrderValue, fixedPrice: f.fixedPrice }
      }
      case 'max_order_fixed': { return { ...base, maximumOrderValue: f.maximumOrderValue, fixedPrice: f.fixedPrice }
      }
      case 'weekday': { return { ...base, dayOfWeek: f.dayOfWeek, isFree: f.isFree, price: f.isFree ? null : f.price }
      }
      case 'region_free': { return { ...base, cepRanges: f.cepRanges.filter((c: any) => c.start && c.end) }
      }
      case 'specific_date': { return { ...base, date: f.date, isFree: f.isFree, price: f.isFree ? null : f.price }
      }
      case 'date_range': { return { ...base, startDate: f.startDate, endDate: f.endDate, isFree: f.isFree, price: f.isFree ? null : f.price }
      }
      default: { return base
      }
    }
  }

  async function save () {
    error.value = ''
    saving.value = true
    try {
      const payload = buildPayload()
      await (editing.value ? api.put(`/rules/${currentId.value}`, payload) : api.post('/rules', payload))
      dialog.value = false
      snackbar.success(editing.value ? 'Regra atualizada.' : 'Regra criada.')
      await load()
    } catch (error_: any) {
      error.value = apiErrorMessage(error_, 'Não foi possível salvar a regra.')
    } finally {
      saving.value = false
    }
  }

  async function toggle (r: Rule) {
    try {
      await api.patch(`/rules/${r.id}/active`, { isActive: !r.isActive })
      snackbar.success(r.isActive ? 'Regra desativada.' : 'Regra ativada.')
      await load()
    } catch (error_: any) {
      snackbar.error(apiErrorMessage(error_))
      await load()
    }
  }

  async function remove (r: Rule) {
    if (!(await confirm.ask({
      title: 'Excluir regra',
      message: `Excluir a regra "${r.name}"?`,
      confirmText: 'Excluir',
    }))) return
    try {
      await api.delete(`/rules/${r.id}`)
      snackbar.warning('Regra excluída.')
      await load()
    } catch (error_: any) {
      snackbar.error(apiErrorMessage(error_))
    }
  }

  load()
</script>
