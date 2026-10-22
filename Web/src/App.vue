<template>
  <v-app>
    <template v-if="auth.isAuthenticated">
      <v-navigation-drawer
        color="surface"
        permanent
        :rail="rail"
        @click="rail = false"
      >
        <v-list-item
          class="py-2"
          nav
          prepend-icon="mdi-truck-fast"
          title="FreightHub"
        >
          <template #append>
            <v-btn
              :icon="rail ? 'mdi-chevron-right' : 'mdi-chevron-left'"
              size="small"
              variant="text"
              @click.stop="rail = !rail"
            />
          </template>
        </v-list-item>

        <v-divider />

        <v-list density="comfortable" nav>
          <v-list-item
            v-for="item in nav"
            :key="item.to"
            :prepend-icon="item.icon"
            :title="item.title"
            :to="item.to"
          />
        </v-list>

        <template #append>
          <v-divider />

          <v-list density="comfortable" nav>
            <v-list-item
              prepend-icon="mdi-key-variant"
              title="Trocar senha"
              @click="pwDialog = true"
            />

            <v-list-item
              prepend-icon="mdi-logout"
              title="Sair"
              @click="logout"
            />
          </v-list>
        </template>
      </v-navigation-drawer>

      <v-app-bar color="primary" flat>
        <v-app-bar-nav-icon @click="rail = !rail" />
        <v-app-bar-title>{{ pageTitle }}</v-app-bar-title>
        <v-spacer />
        <span class="mr-4 text-body-2">{{ auth.user?.name }}</span>
      </v-app-bar>
    </template>

    <v-main>
      <v-container fluid>
        <router-view />
      </v-container>
    </v-main>

    <ChangePasswordDialog v-model="pwDialog" />

    <ConfirmDialog />

    <v-snackbar
      v-model="snackbar.show"
      :color="snackbar.color"
      location="top right"
      :timeout="4000"
    >
      {{ snackbar.text }}
      <template #actions>
        <v-btn icon="mdi-close" size="small" variant="text" @click="snackbar.show = false" />
      </template>
    </v-snackbar>
  </v-app>
</template>

<script lang="ts" setup>
  import { computed, onMounted, ref } from 'vue'
  import { useRoute, useRouter } from 'vue-router'
  import ChangePasswordDialog from '@/components/ChangePasswordDialog.vue'
  import ConfirmDialog from '@/components/ConfirmDialog.vue'
  import { useAuthStore } from '@/stores/auth'
  import { useSnackbar } from '@/stores/snackbar'

  const auth = useAuthStore()
  const snackbar = useSnackbar()
  const router = useRouter()
  const route = useRoute()
  const pwDialog = ref(false)
  const rail = ref(false)

  const nav = [
    { title: 'Painel', icon: 'mdi-view-dashboard', to: '/' },
    { title: 'Regras de frete', icon: 'mdi-ruler-square', to: '/rules' },
    { title: 'Simulador', icon: 'mdi-calculator', to: '/simulator' },
    { title: 'Integrações', icon: 'mdi-connection', to: '/integrations' },
    { title: 'Usuários', icon: 'mdi-account-group', to: '/users' },
    { title: 'Histórico', icon: 'mdi-history', to: '/history' },
  ]

  const pageTitle = computed(() => nav.find(n => n.to === route.path)?.title ?? 'FreightHub')

  function logout () {
    auth.clear()
    router.push('/login')
  }

  onMounted(() => auth.fetchMe())
</script>
