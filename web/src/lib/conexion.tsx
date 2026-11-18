import { useQueryClient } from '@tanstack/react-query'
import { createContext, use, useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { enviar } from './api'
import { pendientes, rechazadas, sincronizar, suscribir, type OperacionRechazada } from './colaOffline'

interface EstadoConexion {
  enLinea: boolean
  porEnviar: number
  rechazos: OperacionRechazada[]
  sincronizando: boolean
  sincronizarAhora: () => Promise<void>
}

const Contexto = createContext<EstadoConexion | null>(null)

/** Observa la señal y vacía la cola sin conexión en cuanto vuelve. */
export function ProveedorConexion({ children }: { children: ReactNode }) {
  const cliente = useQueryClient()
  const [enLinea, setEnLinea] = useState(() => navigator.onLine)
  const [porEnviar, setPorEnviar] = useState(0)
  const [rechazos, setRechazos] = useState<OperacionRechazada[]>([])
  const [sincronizando, setSincronizando] = useState(false)

  const refrescar = useCallback(async () => {
    setPorEnviar((await pendientes()).length)
    setRechazos(await rechazadas())
  }, [])

  const sincronizarAhora = useCallback(async () => {
    if (!navigator.onLine) return
    setSincronizando(true)
    try {
      const resultado = await sincronizar((op) => enviar(op.metodo, op.ruta, op.cuerpo))
      if (resultado.enviadas > 0 || resultado.rechazadas > 0) await cliente.invalidateQueries()
    } finally {
      setSincronizando(false)
      await refrescar()
    }
  }, [cliente, refrescar])

  useEffect(() => {
    const desuscribir = suscribir(() => void refrescar())
    const alConectar = () => {
      setEnLinea(true)
      void sincronizarAhora()
    }
    const alDesconectar = () => setEnLinea(false)
    window.addEventListener('online', alConectar)
    window.addEventListener('offline', alDesconectar)
    void refrescar().then(sincronizarAhora)
    return () => {
      desuscribir()
      window.removeEventListener('online', alConectar)
      window.removeEventListener('offline', alDesconectar)
    }
  }, [refrescar, sincronizarAhora])

  const valor = useMemo(
    () => ({ enLinea, porEnviar, rechazos, sincronizando, sincronizarAhora }),
    [enLinea, porEnviar, rechazos, sincronizando, sincronizarAhora],
  )
  return <Contexto value={valor}>{children}</Contexto>
}

export function useConexion() {
  const contexto = use(Contexto)
  if (!contexto) throw new Error('useConexion debe usarse dentro de <ProveedorConexion>')
  return contexto
}
