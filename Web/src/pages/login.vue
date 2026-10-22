<template>
  <v-row align="center" class="fill-height" justify="center" style="min-height: 80vh">
    <v-col cols="12" md="4" sm="8">
      <v-card class="pa-4" elevation="4">
        <div class="text-center mb-4">
          <v-icon color="primary" icon="mdi-truck-fast" size="48" />
          <h2 class="text-h6 mt-2">FreightHub</h2>
          <p class="text-body-2 text-medium-emphasis">Portal administrativo</p>
        </div>

        <v-alert v-if="error" class="mb-4" density="compact" type="error">{{ error }}</v-alert>

        <v-form @submit.prevent="submit">
          <v-text-field
            v-model="email"
            class="mb-2"
            label="E-mail"
            prepend-inner-icon="mdi-email"
            type="email"
          />

          <v-text-field
            v-model="password"
            label="Senha"
            prepend-inner-icon="mdi-lock"
            type="password"
          />

          <v-btn
            block
            class="mt-4"
            color="primary"
            :loading="loading"
            size="large"
            text="Entrar"
            type="submit"
          />
        </v-form>
      </v-card>
    </v-col>
  </v-row>
</template>

<script lang="ts" setup>
  import { ref } from 'vue'
  import { useRouter } from 'vue-router'
  import { useAuthStore } from '@/stores/auth'

  const auth = useAuthStore()
  const router = useRouter()

  const email = ref('')
  const password = ref('')
  const error = ref('')
  const loading = ref(false)

  async function submit () {
    error.value = ''
    loading.value = true
    try {
      await auth.login(email.value, password.value)
      router.push('/')
    } catch (error_: any) {
      error.value = error_.response?.status === 401
        ? 'Credenciais inválidas.'
        : 'Não foi possível entrar. Verifique se a API está no ar.'
    } finally {
      loading.value = false
    }
  }
</script>
