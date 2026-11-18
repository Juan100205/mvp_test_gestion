import { describe, expect, it } from 'vitest'
import { ApiError, mensajeDeError } from './api'
import { mesActual, mesesRecientes, nombreMes, semanaDelMes } from './periodo'

describe('periodos', () => {
  it('el mes actual usa el formato AAAA-MM del API', () => {
    expect(mesActual(new Date(2026, 10, 18))).toBe('2026-11')
    expect(mesActual(new Date(2026, 0, 3))).toBe('2026-01')
  })

  it('nombra el mes en español', () => {
    expect(nombreMes('2026-11')).toBe('noviembre de 2026')
  })

  it('la semana del mes va de 1 a 5', () => {
    expect(semanaDelMes(new Date(2026, 10, 1))).toBe(1)
    expect(semanaDelMes(new Date(2026, 10, 18))).toBe(3)
    expect(semanaDelMes(new Date(2026, 10, 30))).toBe(5)
  })

  it('lista los meses recientes del más nuevo al más viejo', () => {
    expect(mesesRecientes(3, new Date(2026, 0, 15))).toEqual(['2026-01', '2025-12', '2025-11'])
  })
})

describe('mensajes de error', () => {
  it('usa el detalle que envía el API', () => {
    const error = new ApiError(409, { codigo: 'estrategia_bloqueada', detail: 'Solo consulta.' })
    expect(mensajeDeError(error)).toBe('Solo consulta.')
  })

  it('explica la falta de conexión sin tecnicismos', () => {
    expect(mensajeDeError(new TypeError('Failed to fetch'))).toMatch(/no hay conexión/i)
  })

  it('expone los campos a corregir', () => {
    const error = new ApiError(400, { codigo: 'datos_incompletos', detail: 'x', campos: ['nombre', 'periodo'] })
    expect(error.campos).toEqual(['nombre', 'periodo'])
    expect(error.codigo).toBe('datos_incompletos')
  })
})
