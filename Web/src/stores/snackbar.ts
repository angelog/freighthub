import { defineStore } from 'pinia'
import { ref } from 'vue'

export type SnackColor = 'success' | 'error' | 'warning' | 'info'

export const useSnackbar = defineStore('snackbar', () => {
  const show = ref(false)
  const text = ref('')
  const color = ref<SnackColor>('success')

  function notify (message: string, c: SnackColor) {
    text.value = message
    color.value = c
    show.value = true
  }

  const success = (m: string) => notify(m, 'success')
  const error = (m: string) => notify(m, 'error')
  const warning = (m: string) => notify(m, 'warning')
  const info = (m: string) => notify(m, 'info')

  return { show, text, color, success, error, warning, info }
})
