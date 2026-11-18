const dosDigitos = (n: number) => String(n).padStart(2, '0')

/** Mes en el formato del API: AAAA-MM. */
export const mesActual = (fecha = new Date()) => `${fecha.getFullYear()}-${dosDigitos(fecha.getMonth() + 1)}`

export function nombreMes(periodo: string) {
  const [anio, mes] = periodo.split('-').map(Number)
  const fecha = new Date(anio ?? 2026, (mes ?? 1) - 1, 1)
  return fecha.toLocaleDateString('es-CO', { month: 'long', year: 'numeric' })
}

/** Semana del mes de 1 a 5 (días 1-7, 8-14, 15-21, 22-28, 29-31). */
export const semanaDelMes = (fecha = new Date()) => Math.min(5, Math.ceil(fecha.getDate() / 7))

export function mesesRecientes(cantidad: number, desde = new Date()) {
  return Array.from({ length: cantidad }, (_, i) => mesActual(new Date(desde.getFullYear(), desde.getMonth() - i, 1)))
}

export const periodosAcademicos = (anio = new Date().getFullYear()) => [`${anio}-1`, `${anio}-2`, `${anio + 1}-1`]

export const hora = (iso: string) => new Date(iso).toLocaleTimeString('es-CO', { hour: 'numeric', minute: '2-digit' })

export const fechaCorta = (iso: string) =>
  new Date(iso).toLocaleDateString('es-CO', { day: 'numeric', month: 'short', hour: 'numeric', minute: '2-digit' })
