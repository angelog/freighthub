<template>
  <div>
    <h1 class="text-h5 mb-4">Integrações</h1>

    <v-card class="mb-6" title="Transportadoras">
      <v-list>
        <v-list-item
          v-for="c in carriers"
          :key="c.carrier"
          :subtitle="c.available ? 'Disponível' : 'Não configurada (sem BaseUrl)'"
          :title="c.carrier"
        >
          <template #append>
            <v-switch
              color="primary"
              :disabled="!c.available"
              hide-details
              :model-value="c.enabled"
              @update:model-value="toggle(c)"
            />
          </template>
        </v-list-item>
      </v-list>
    </v-card>

    <v-card>
      <v-toolbar color="surface" density="comfortable">
        <v-toolbar-title>Credenciais</v-toolbar-title>
        <v-spacer />
        <v-btn color="primary" prepend-icon="mdi-plus" text="Nova credencial" @click="openNew" />
      </v-toolbar>

      <v-data-table
        :headers="credHeaders"
        :items="credentials"
        :loading="loadingCreds"
        :sort-by="[{ key: 'integration', order: 'asc' }]"
      >
        <template #item.updatedAt="{ value }">{{ new Date(value).toLocaleString('pt-BR') }}</template>

        <template #item.isActive="{ value }">
          <v-chip :color="value ? 'success' : 'default'" size="small">{{ value ? 'Ativa' : 'Inativa' }}</v-chip>
        </template>

        <template #item.actions="{ item }">
          <v-btn icon="mdi-pencil" size="small" variant="text" @click="openEdit(item)" />

          <v-btn
            color="error"
            icon="mdi-delete"
            size="small"
            variant="text"
            @click="removeCred(item)"
          />
        </template>
      </v-data-table>
    </v-card>

    <v-dialog v-model="dialog" max-width="520">
      <v-card :title="editing ? 'Editar credencial' : 'Nova credencial'">
        <v-card-text>
          <v-row>
            <v-col cols="12" sm="6">
              <v-select v-model="form.integration" :disabled="editing" :items="['Correios', 'Loggi', 'Yampi', 'Fretops']" label="Integração" />
            </v-col>

            <v-col cols="12" sm="6">
              <v-select v-model="form.environment" :disabled="editing" :items="['Homolog', 'Production']" label="Ambiente" />
            </v-col>
          </v-row>

          <v-text-field v-model="form.key" autocomplete="off" class="mb-2" :disabled="editing" label="Chave (ex.: AccessKey)" name="credential-key" />

          <v-text-field
            v-model="form.value"
            :append-inner-icon="showValue ? 'mdi-eye-off' : 'mdi-eye'"
            autocomplete="new-password"
            label="Valor"
            :loading="loadingValue"
            name="credential-value"
            :type="showValue ? 'text' : 'password'"
            @click:append-inner="showValue = !showValue"
          />

          <p class="text-caption text-medium-emphasis">O valor é cifrado no banco.</p>
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

  interface Credential {
    integration: string
    environment: string
    key: string
    isActive: boolean
    updatedAt: string
  }

  const snackbar = useSnackbar()
  const confirm = useConfirm()

  const carriers = ref<any[]>([])
  const credentials = ref<Credential[]>([])
  const loadingCreds = ref(false)

  const dialog = ref(false)
  const editing = ref(false)
  const saving = ref(false)
  const loadingValue = ref(false)
  const showValue = ref(false)
  const form = ref({ integration: 'Correios', environment: 'Production', key: '', value: '' })

  const credHeaders = [
    { title: 'Integração', key: 'integration' },
    { title: 'Ambiente', key: 'environment' },
    { title: 'Chave', key: 'key' },
    { title: 'Atualizado em', key: 'updatedAt' },
    { title: 'Situação', key: 'isActive' },
    { title: '', key: 'actions', sortable: false, align: 'end' as const },
  ]

  async function loadCarriers () {
    carriers.value = (await api.get('/admin/carriers')).data
  }

  async function loadCredentials () {
    loadingCreds.value = true
    try {
      credentials.value = (await api.get('/admin/credentials')).data
    } finally {
      loadingCreds.value = false
    }
  }

  async function toggle (c: any) {
    try {
      await api.put(`/admin/carriers/${c.carrier}`, { isEnabled: !c.enabled })
      snackbar.success(`${c.carrier} ${c.enabled ? 'desativada' : 'ativada'}.`)
      await loadCarriers()
    } catch (error_: any) {
      snackbar.error(apiErrorMessage(error_))
      await loadCarriers()
    }
  }

  function openNew () {
    editing.value = false
    showValue.value = false
    form.value = { integration: 'Correios', environment: 'Production', key: '', value: '' }
    dialog.value = true
  }

  async function openEdit (c: Credential) {
    editing.value = true
    showValue.value = false
    form.value = { integration: c.integration, environment: c.environment, key: c.key, value: '' }
    dialog.value = true

    // Busca o valor atual (decifrado) para edição.
    loadingValue.value = true
    try {
      const { data } = await api.get(`/admin/credentials/${c.integration}/${c.environment}/${c.key}/value`)
      form.value.value = data.value
    } catch (error_: any) {
      snackbar.error(apiErrorMessage(error_, 'Não foi possível carregar o valor atual.'))
    } finally {
      loadingValue.value = false
    }
  }

  async function save () {
    if (!form.value.key || !form.value.value) {
      snackbar.warning('Preencha a chave e o valor.')
      return
    }
    saving.value = true
    try {
      await api.put('/admin/credentials', form.value)
      snackbar.success(editing.value ? 'Credencial atualizada.' : 'Credencial cadastrada.')
      dialog.value = false
      await loadCredentials()
    } catch (error_: any) {
      snackbar.error(apiErrorMessage(error_, 'Não foi possível salvar a credencial.'))
    } finally {
      saving.value = false
    }
  }

  async function removeCred (c: Credential) {
    if (!(await confirm.ask({
      title: 'Excluir credencial',
      message: `Excluir a credencial ${c.integration} / ${c.key}?`,
      confirmText: 'Excluir',
    }))) return
    try {
      await api.delete(`/admin/credentials/${c.integration}/${c.environment}/${c.key}`)
      snackbar.warning('Credencial excluída.')
      await loadCredentials()
    } catch (error_: any) {
      snackbar.error(apiErrorMessage(error_, 'Não foi possível excluir a credencial.'))
    }
  }

  loadCarriers()
  loadCredentials()
</script>
