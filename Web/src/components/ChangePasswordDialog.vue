<template>
  <v-dialog v-model="model" max-width="440">
    <v-card title="Trocar senha">
      <v-card-text>
        <v-alert v-if="error" class="mb-4" density="compact" type="error">{{ error }}</v-alert>

        <v-text-field
          v-model="current"
          autocomplete="current-password"
          class="mb-2"
          label="Senha atual"
          type="password"
        />

        <v-text-field
          v-model="next"
          autocomplete="new-password"
          hint="Mínimo 8 caracteres"
          label="Nova senha"
          type="password"
        />
      </v-card-text>

      <v-card-actions>
        <v-spacer />
        <v-btn text="Cancelar" @click="close" />

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
</template>

<script lang="ts" setup>
  import { ref, watch } from 'vue'
  import { api } from '@/api/client'

  const model = defineModel<boolean>({ required: true })

  const current = ref('')
  const next = ref('')
  const error = ref('')
  const saving = ref(false)

  // Limpa o formulário sempre que o diálogo abre.
  watch(model, open => {
    if (open) {
      current.value = ''
      next.value = ''
      error.value = ''
    }
  })

  function close () {
    model.value = false
  }

  async function save () {
    error.value = ''
    if (next.value.length < 8) {
      error.value = 'A nova senha deve ter ao menos 8 caracteres.'
      return
    }
    saving.value = true
    try {
      await api.post('/auth/change-password', { currentPassword: current.value, newPassword: next.value })
      close()
    } catch (error_: any) {
      error.value = error_.response?.data?.detail ?? 'Não foi possível trocar a senha.'
    } finally {
      saving.value = false
    }
  }
</script>
