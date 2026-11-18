import '@fontsource-variable/inter'
import '@fontsource-variable/space-grotesk'
import './index.css'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router'
import { Rutas } from './App'
import { ApiError } from './lib/api'
import { ProveedorConexion } from './lib/conexion'
import { ProveedorSesion } from './lib/sesion'

const cliente = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      // Los errores 4xx son definitivos; reintentar solo tiene sentido con fallas de red o del servidor.
      retry: (intentos, error) => !(error instanceof ApiError && error.status < 500) && intentos < 2,
    },
  },
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={cliente}>
      <ProveedorSesion>
        <ProveedorConexion>
          <BrowserRouter>
            <Rutas />
          </BrowserRouter>
        </ProveedorConexion>
      </ProveedorSesion>
    </QueryClientProvider>
  </StrictMode>,
)
