import { useEffect, useRef, type ReactNode } from 'react'
import { Icono } from './Icono'

interface Props {
  abierta: boolean
  titulo: string
  subtitulo?: string
  onCerrar: () => void
  children: ReactNode
}

/** Hoja inferior en celular y diálogo centrado en escritorio. Usa <dialog> nativo: foco atrapado y Esc para cerrar. */
export function Hoja({ abierta, titulo, subtitulo, onCerrar, children }: Props) {
  const ref = useRef<HTMLDialogElement>(null)

  useEffect(() => {
    const dialogo = ref.current
    if (!dialogo) return
    if (abierta && !dialogo.open) dialogo.showModal()
    if (!abierta && dialogo.open) dialogo.close()
  }, [abierta])

  return (
    <dialog
      ref={ref}
      onClose={onCerrar}
      onClick={(e) => e.target === ref.current && onCerrar()}
      aria-labelledby="hoja-titulo"
      className="m-0 mt-auto w-full max-w-none animate-entrar-hoja rounded-t-hoja bg-papel p-0 text-tinta backdrop:bg-tinta/45
        sm:m-auto sm:max-w-lg sm:rounded-hoja"
    >
      <div className="max-h-[88dvh] overflow-y-auto px-5 pb-[max(1.25rem,env(safe-area-inset-bottom))] pt-5 sm:px-7 sm:pb-7">
        <header className="mb-5 flex items-start justify-between gap-4">
          <div>
            <h2 id="hoja-titulo" className="text-xl font-semibold">
              {titulo}
            </h2>
            {subtitulo && <p className="mt-1 text-sm text-niebla">{subtitulo}</p>}
          </div>
          <button type="button" onClick={onCerrar} className="-m-2 rounded-control p-2 text-niebla hover:text-tinta">
            <Icono nombre="cerrar" />
            <span className="sr-only">Cerrar</span>
          </button>
        </header>
        {children}
      </div>
    </dialog>
  )
}
