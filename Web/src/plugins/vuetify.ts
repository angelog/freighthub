/**
 * plugins/vuetify.ts
 */

import { createVuetify } from 'vuetify'
import '@mdi/font/css/materialdesignicons.css'
import 'vuetify/styles'

export default createVuetify({
  theme: {
    defaultTheme: 'light',
    themes: {
      light: {
        colors: {
          primary: '#000',
          secondary: '#00897B',
          surface: '#FFFFFF',
          background: '#F5F7FA',
          error: '#C62828',
          success: '#2E7D32',
          warning: '#EF6C00',
        },
      },
    },
  },
})
