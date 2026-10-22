import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { api } from '@/api/client'

export interface AuthUser {
  id: string
  name: string
  email: string
  isActive: boolean
}

export const useAuthStore = defineStore('auth', () => {
  const token = ref<string | null>(localStorage.getItem('token'))
  const user = ref<AuthUser | null>(null)
  const isAuthenticated = computed(() => !!token.value)

  async function login (email: string, password: string) {
    const { data } = await api.post('/auth/login', { email, password })
    token.value = data.token
    localStorage.setItem('token', data.token)
    user.value = data.user
  }

  async function fetchMe () {
    if (!token.value) {
      return
    }
    try {
      const { data } = await api.get('/auth/me')
      user.value = data
    } catch {
      clear()
    }
  }

  function clear () {
    token.value = null
    user.value = null
    localStorage.removeItem('token')
  }

  return { token, user, isAuthenticated, login, fetchMe, clear }
})
