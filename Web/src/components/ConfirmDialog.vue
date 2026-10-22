<template>
  <v-dialog v-model="show" max-width="420" persistent>
    <v-card :title="options.title ?? 'Confirmar'">
      <v-card-text>{{ options.message }}</v-card-text>

      <v-card-actions>
        <v-spacer />
        <v-btn :text="options.cancelText ?? 'Cancelar'" @click="confirm.respond(false)" />

        <v-btn
          :color="options.color ?? 'error'"
          :text="options.confirmText ?? 'Excluir'"
          variant="flat"
          @click="confirm.respond(true)"
        />
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script lang="ts" setup>
  import { storeToRefs } from 'pinia'
  import { useConfirm } from '@/stores/confirm'

  const confirm = useConfirm()
  const { show, options } = storeToRefs(confirm)
</script>
