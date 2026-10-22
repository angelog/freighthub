/**
 * router/index.ts
 *
 * Automatic routes for ./src/pages/*.vue
 */

import { createRouter, createWebHistory } from 'vue-router'
import { routes } from 'vue-router/auto-routes'
import { useAuthStore } from '@/stores/auth'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
})

// Guarda de autenticação: tudo exige login, exceto /login.
router.beforeEach(to => {
  const auth = useAuthStore()
  if (to.path !== '/login' && !auth.isAuthenticated) {
    return { path: '/login' }
  }
  if (to.path === '/login' && auth.isAuthenticated) {
    return { path: '/' }
  }
})

export default router
