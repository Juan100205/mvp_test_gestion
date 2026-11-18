import { createContext, use, useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { api, configurarExpiracion, guardarSesion, leerSesion } from './api'
import type { Sesion } from './tipos'

interface ContextoSesion {
  sesion: Sesion | null
  iniciar: (username: string, password: string) => Promise<Sesion>
  cerrar: () => void
}

const Contexto = createContext<ContextoSesion | null>(null)

export function ProveedorSesion({ children }: { children: ReactNode }) {
  const [sesion, setSesion] = useState<Sesion | null>(() => leerSesion())

  const cerrar = useCallback(() => {
    guardarSesion(null)
    setSesion(null)
  }, [])

  useEffect(() => configurarExpiracion(cerrar), [cerrar])

  const iniciar = useCallback(async (username: string, password: string) => {
    const nueva = await api<Sesion>('/api/auth/login', { metodo: 'POST', cuerpo: { username, password } })
    guardarSesion(nueva)
    setSesion(nueva)
    return nueva
  }, [])

  const valor = useMemo(() => ({ sesion, iniciar, cerrar }), [sesion, iniciar, cerrar])
  return <Contexto value={valor}>{children}</Contexto>
}

export function useSesion() {
  const contexto = use(Contexto)
  if (!contexto) throw new Error('useSesion debe usarse dentro de <ProveedorSesion>')
  return contexto
}
