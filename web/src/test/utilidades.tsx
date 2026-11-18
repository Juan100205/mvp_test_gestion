import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import type { ReactNode } from 'react'
import { MemoryRouter } from 'react-router'
import { vi } from 'vitest'
import { ProveedorConexion } from '../lib/conexion'
import { ProveedorSesion } from '../lib/sesion'
import type { Rol, Sesion } from '../lib/tipos'

export function sesionDe(rol: Rol, username = 'tutor.caribe'): Sesion {
  return {
    token: 'jwt-prueba',
    expira: '2099-01-01T00:00:00Z',
    usuario: { id: 1, username, nombre: 'Laura Gómez Palacios', rol, region: 'Caribe' },
  }
}

export const iniciarSesionComo = (rol: Rol) => localStorage.setItem('sses.sesion', JSON.stringify(sesionDe(rol)))

type Respuesta = { status?: number; cuerpo?: unknown } | ((cuerpo: unknown, url: URL) => { status?: number; cuerpo?: unknown })

export interface Llamada {
  metodo: string
  ruta: string
  url: URL
  cuerpo: unknown
}

/**
 * Simula el API por "MÉTODO /ruta" (sin query string). Lo no declarado responde 404.
 * Devuelve las llamadas recibidas para verificar qué envió la interfaz.
 */
export function simularApi(rutas: Record<string, Respuesta>) {
  const llamadas: Llamada[] = []
  vi.spyOn(globalThis, 'fetch').mockImplementation(async (entrada, init) => {
    const url = new URL(typeof entrada === 'string' ? entrada : entrada instanceof URL ? entrada.href : entrada.url, 'http://localhost')
    const metodo = init?.method ?? 'GET'
    const cuerpo = init?.body ? JSON.parse(String(init.body)) : undefined
    llamadas.push({ metodo, ruta: url.pathname, url, cuerpo })

    const definida = rutas[`${metodo} ${url.pathname}`]
    const { status = 200, cuerpo: respuesta } =
      typeof definida === 'function' ? definida(cuerpo, url) : (definida ?? { status: 404, cuerpo: { codigo: 'no_encontrado', detail: 'No encontrado' } })
    return new Response(respuesta === undefined ? null : JSON.stringify(respuesta), {
      status,
      headers: { 'Content-Type': 'application/json' },
    })
  })
  return llamadas
}

export function pintar(ui: ReactNode, { ruta = '/' }: { ruta?: string } = {}) {
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return render(
    <QueryClientProvider client={cliente}>
      <ProveedorSesion>
        <ProveedorConexion>
          <MemoryRouter initialEntries={[ruta]}>{ui}</MemoryRouter>
        </ProveedorConexion>
      </ProveedorSesion>
    </QueryClientProvider>,
  )
}

export function simularSinSenal() {
  Object.defineProperty(navigator, 'onLine', { configurable: true, get: () => false })
  return () => Object.defineProperty(navigator, 'onLine', { configurable: true, get: () => true })
}

/** Datos de ejemplo coherentes con DatosPrecargados del backend. */
export const ejemplo = {
  entidades: [
    { id: 1, nombre: 'IE Técnica Simón Bolívar', region: 'Caribe', municipio: 'Barranquilla' },
    { id: 2, nombre: 'IE San José de Turbaco', region: 'Caribe', municipio: 'Turbaco' },
    { id: 4, nombre: 'IE Normal Superior de Quibdó', region: 'Pacífico', municipio: 'Quibdó' },
  ],
  docentes: [
    { id: 1, documento: '52000000', nombre: 'Marta Lucía Ruiz' },
    { id: 2, documento: '52007919', nombre: 'Óscar Iván Peña' },
    { id: 3, documento: '52015838', nombre: 'Claudia Patricia Mena' },
  ],
  estrategia(cambios: Record<string, unknown> = {}) {
    return {
      id: 7,
      nombre: 'Club de lectura en voz alta',
      descripcion: 'Lectura compartida los martes.',
      periodo: '2026-2',
      entidad: ejemplo.entidades[0],
      estado: 'Editable',
      esEditable: true,
      docentes: ejemplo.docentes.slice(0, 2),
      estudiantes: [],
      ...cambios,
    }
  },
  acompanamiento(cambios: Record<string, unknown> = {}) {
    return {
      id: 'a1',
      estrategiaId: 7,
      tipo: 'Individual',
      periodo: '2026-11',
      objetivo: 'Mejorar la planeación de clase',
      compromisos: '',
      docentes: [ejemplo.docentes[0]],
      avanceActual: 40,
      seguimientos: [
        { id: 's1', semana: 1, actividades: 'Revisión de planeador', avance: 20, observaciones: '', actualizadoEn: '2026-11-03T10:00:00Z' },
        { id: 's2', semana: 2, actividades: 'Observación de aula', avance: 40, observaciones: '', actualizadoEn: '2026-11-10T10:00:00Z' },
      ],
      registradoEn: '2026-11-02T10:00:00Z',
      existente: false,
      ...cambios,
    }
  },
}
