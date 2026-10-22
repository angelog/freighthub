import { defineStore } from 'pinia'
import { ref } from 'vue'

export interface ConfirmOptions {
  title?: string
  message: string
  confirmText?: string
  cancelText?: string
  color?: string
}

/**
 * Diálogo de confirmação global, baseado em promise.
 * Uso: `if (!(await confirm.ask({ message: '...' }))) return`
 */
export const useConfirm = defineStore('confirm', () => {
  const show = ref(false)
  const options = ref<ConfirmOptions>({ message: '' })
  let resolver: ((value: boolean) => void) | null = null

  function ask (opts: ConfirmOptions): Promise<boolean> {
    options.value = opts
    show.value = true
    return new Promise<boolean>(resolve => {
      resolver = resolve
    })
  }

  function respond (value: boolean) {
    show.value = false
    resolver?.(value)
    resolver = null
  }

  return { show, options, ask, respond }
})
