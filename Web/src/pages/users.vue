<template>
  <div>
    <div class="d-flex align-center mb-4">
      <h1 class="text-h5">Usuários</h1>
      <v-spacer />
      <v-btn color="primary" prepend-icon="mdi-plus" text="Novo usuário" @click="openNew" />
    </div>

    <v-card>
      <v-data-table :headers="headers" :items="users" :loading="loading">
        <template #item.isActive="{ value }">
          <v-chip :color="value ? 'success' : 'default'" size="small">{{ value ? 'Ativo' : 'Inativo' }}</v-chip>
        </template>

        <template #item.actions="{ item }">
          <v-btn icon="mdi-pencil" size="small" variant="text" @click="openEdit(item)" />

          <v-btn
            color="error"
            :disabled="item.id === auth.user?.id"
            icon="mdi-delete"
            size="small"
            :title="item.id === auth.user?.id ? 'Você não pode excluir o próprio usuário' : 'Excluir'"
            variant="text"
            @click="remove(item)"
          />
        </template>
      </v-data-table>
    </v-card>

    <v-dialog v-model="dialog" max-width="480">
      <v-card :title="editing ? 'Editar usuário' : 'Novo usuário'">
        <v-card-text>
          <v-alert v-if="error" class="mb-4" density="compact" type="error">{{ error }}</v-alert>
          <v-text-field v-model="form.name" autocomplete="off" class="mb-2" label="Nome" name="user-name" />
          <v-text-field v-model="form.email" autocomplete="off" class="mb-2" label="E-mail" name="user-email" type="email" />

          <v-text-field
            v-model="form.password"
            :append-inner-icon="showPassword ? 'mdi-eye-off' : 'mdi-eye'"
            autocomplete="new-password"
            class="mb-2"
            :hint="editing ? 'Deixe em branco para manter a senha atual' : 'Mínimo 8 caracteres'"
            label="Senha"
            name="user-password"
            persistent-hint
            :type="showPassword ? 'text' : 'password'"
            @click:append-inner="showPassword = !showPassword"
          />

          <v-switch v-if="editing" v-model="form.isActive" color="primary" label="Ativo" />
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
  import { useAuthStore } from '@/stores/auth'
  import { useConfirm } from '@/stores/confirm'
  import { useSnackbar } from '@/stores/snackbar'

  const auth = useAuthStore()
  const confirm = useConfirm()
  const snackbar = useSnackbar()

  interface User { id: string, name: string, email: string, isActive: boolean }

  const headers = [
    { title: 'Nome', key: 'name' },
    { title: 'E-mail', key: 'email' },
    { title: 'Situação', key: 'isActive' },
    { title: '', key: 'actions', sortable: false, align: 'end' as const },
  ]

  const users = ref<User[]>([])
  const loading = ref(false)
  const dialog = ref(false)
  const saving = ref(false)
  const editing = ref(false)
  const error = ref('')
  const showPassword = ref(false)
  const currentId = ref<string | null>(null)
  const form = ref({ name: '', email: '', password: '', isActive: true })

  async function load () {
    loading.value = true
    try {
      users.value = (await api.get('/users')).data
    } finally {
      loading.value = false
    }
  }

  function openNew () {
    editing.value = false
    currentId.value = null
    showPassword.value = false
    form.value = { name: '', email: '', password: '', isActive: true }
    error.value = ''
    dialog.value = true
  }

  function openEdit (user: User) {
    editing.value = true
    currentId.value = user.id
    showPassword.value = false
    form.value = { name: user.name, email: user.email, password: '', isActive: user.isActive }
    error.value = ''
    dialog.value = true
  }

  async function save () {
    error.value = ''
    saving.value = true
    try {
      await (editing.value
        ? api.put(`/users/${currentId.value}`, {
          name: form.value.name,
          email: form.value.email,
          isActive: form.value.isActive,
          password: form.value.password || null,
        })
        : api.post('/users', {
          name: form.value.name,
          email: form.value.email,
          password: form.value.password,
        }))
      dialog.value = false
      snackbar.success(editing.value ? 'Usuário atualizado.' : 'Usuário criado.')
      await load()
    } catch (error_: any) {
      error.value = apiErrorMessage(error_, 'Não foi possível salvar o usuário.')
    } finally {
      saving.value = false
    }
  }

  async function remove (user: User) {
    if (user.id === auth.user?.id) {
      snackbar.warning('Você não pode excluir o próprio usuário.')
      return
    }
    if (!(await confirm.ask({
      title: 'Excluir usuário',
      message: `Excluir o usuário "${user.name}"? Esta ação não pode ser desfeita.`,
      confirmText: 'Excluir',
    }))) return
    try {
      await api.delete(`/users/${user.id}`)
      snackbar.warning('Usuário excluído.')
      await load()
    } catch (error_: any) {
      snackbar.error(apiErrorMessage(error_, 'Não foi possível excluir o usuário.'))
    }
  }

  load()
</script>
