import axios from 'axios'
import router from '@/router'
import { useAuthStore } from '@/stores/auth'

const baseURL = import.meta.env.VITE_API_URL ?? 'http://localhost:5066/api/v1'

export const api = axios.create({ baseURL })

// Anexa o token JWT em toda requisição.
api.interceptors.request.use(config => {
  const token = localStorage.getItem('token')
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

// 401 → limpa sessão e volta ao login.
api.interceptors.response.use(
  response => response,
  error => {
    if (error.response?.status === 401) {
      const auth = useAuthStore()
      auth.clear()
      if (router.currentRoute.value.path !== '/login') {
        router.push('/login')
      }
    }
    return Promise.reject(error)
  },
)
