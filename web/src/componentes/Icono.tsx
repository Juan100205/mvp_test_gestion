const trazos = {
  candado: 'M7 11V8a5 5 0 0 1 10 0v3M5.5 11h13v9.5h-13z',
  mas: 'M12 5v14M5 12h14',
  atras: 'M15 18l-6-6 6-6',
  buscar: 'M11 18a7 7 0 1 0 0-14 7 7 0 0 0 0 14zM20 20l-4-4',
  senal: 'M4 18h.01M8 18v-3M12 18v-6M16 18V9M20 18V6',
  sinSenal: 'M4 18h.01M8 18v-3M12 18v-6M3 3l18 18',
  nube: 'M7 18h10a4 4 0 0 0 .5-7.97A6 6 0 0 0 6 11a3.5 3.5 0 0 0 1 7z',
  salir: 'M15 4h4v16h-4M10 8l-4 4 4 4M6 12h10',
  estrategias: 'M4 5h16M4 12h16M4 19h10',
  registro: 'M9 5H6v15h12V5h-3M9 3h6v4H9zM9 12h6M9 16h4',
  cerrar: 'M6 6l12 12M18 6L6 18',
  usuario: 'M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM4 21a8 8 0 0 1 16 0',
  check: 'M5 12.5l4.5 4.5L19 7.5',
} as const

export type NombreIcono = keyof typeof trazos

export function Icono({ nombre, className = 'size-5' }: { nombre: NombreIcono; className?: string }) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.8}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden
      className={className}
    >
      <path d={trazos[nombre]} />
    </svg>
  )
}
