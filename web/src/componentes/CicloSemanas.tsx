import { Acompanamiento } from './constantes'

interface SemanaRegistrada {
  semana: number
  avance: number
  pendiente?: boolean
}

interface Props {
  seguimientos: SemanaRegistrada[]
  semanaActual?: number
  onElegir: (semana: number) => void
  compacto?: boolean
}

/**
 * El mes del acompañamiento como un perfil de terreno: cada columna es una semana
 * y se llena con el avance registrado. Es la "periodicidad mensual con seguimiento semanal" del pliego.
 */
export function CicloSemanas({ seguimientos, semanaActual, onElegir, compacto = false }: Props) {
  const porSemana = new Map(seguimientos.map((s) => [s.semana, s]))

  return (
    <ol className={`grid grid-cols-5 ${compacto ? 'gap-1' : 'gap-1.5 sm:gap-2'}`}>
      {Array.from({ length: Acompanamiento.semanasPorMes }, (_, i) => i + 1).map((semana) => {
        const registro = porSemana.get(semana)
        const esActual = semana === semanaActual
        const estado = registro
          ? `${registro.avance} %${registro.pendiente ? ', pendiente de enviar' : ''}`
          : 'sin registrar'

        return (
          <li key={semana}>
            <button
              type="button"
              onClick={() => onElegir(semana)}
              aria-label={`Semana ${semana}: ${estado}`}
              aria-current={esActual ? 'date' : undefined}
              className="group flex w-full flex-col gap-1.5 text-left"
            >
              <span
                className={`relative block w-full overflow-hidden rounded-[5px] transition-colors ${
                  compacto ? 'h-9' : 'h-20 sm:h-24'
                } ${
                  registro
                    ? 'bg-duna-suave'
                    : 'border border-dashed border-duna bg-transparent group-hover:border-niebla'
                }`}
              >
                {registro && (
                  <span
                    className={`absolute inset-x-0 bottom-0 transition-[height] duration-500 ${
                      registro.pendiente
                        ? 'bg-[repeating-linear-gradient(135deg,var(--color-cielo)_0_4px,#5d8bb6_4px_8px)]'
                        : 'bg-terracota'
                    }`}
                    style={{ height: `${Math.max(registro.avance, 6)}%` }}
                  />
                )}
                {!compacto && registro && (
                  <span className="cifra absolute left-1.5 top-1 text-xs font-semibold text-tinta">{registro.avance}</span>
                )}
              </span>
              {!compacto && (
                <span
                  className={`flex items-center gap-1 text-xs ${esActual ? 'font-semibold text-tinta' : 'text-niebla'}`}
                >
                  {esActual && <span aria-hidden className="size-1.5 rounded-full bg-terracota" />}
                  Sem. {semana}
                </span>
              )}
            </button>
          </li>
        )
      })}
    </ol>
  )
}
