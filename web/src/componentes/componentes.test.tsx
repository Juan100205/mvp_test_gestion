import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { CicloSemanas } from './CicloSemanas'
import { SelloEstado } from './SelloEstado'

describe('CicloSemanas (CU-03: periodicidad mensual con seguimiento semanal)', () => {
  const seguimientos = [
    { semana: 1, avance: 20 },
    { semana: 2, avance: 45 },
  ]

  it('muestra las cinco semanas del mes', () => {
    render(<CicloSemanas seguimientos={seguimientos} onElegir={() => {}} />)
    expect(screen.getAllByRole('button')).toHaveLength(5)
  })

  it('las semanas registradas muestran su avance y las demás invitan a registrar', () => {
    render(<CicloSemanas seguimientos={seguimientos} onElegir={() => {}} />)
    expect(screen.getByRole('button', { name: /semana 2.*45 %/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /semana 4.*sin registrar/i })).toBeInTheDocument()
  })

  it('marca la semana en curso', () => {
    render(<CicloSemanas seguimientos={seguimientos} semanaActual={3} onElegir={() => {}} />)
    expect(screen.getByRole('button', { name: /semana 3/i })).toHaveAttribute('aria-current', 'date')
  })

  it('al tocar una semana pide registrar o actualizar su seguimiento', async () => {
    const onElegir = vi.fn()
    render(<CicloSemanas seguimientos={seguimientos} onElegir={onElegir} />)

    await userEvent.click(screen.getByRole('button', { name: /semana 2/i }))

    expect(onElegir).toHaveBeenCalledWith(2)
  })

  it('indica las semanas pendientes de sincronizar', () => {
    render(<CicloSemanas seguimientos={[{ semana: 1, avance: 10, pendiente: true }]} onElegir={() => {}} />)
    expect(screen.getByRole('button', { name: /semana 1.*pendiente de enviar/i })).toBeInTheDocument()
  })
})

describe('SelloEstado (RQ_03)', () => {
  it('una estrategia sin estudiantes se puede editar', () => {
    render(<SelloEstado estado="Editable" />)
    expect(screen.getByText('Editable')).toBeInTheDocument()
  })

  it('con estudiantes vinculados queda solo para consulta', () => {
    render(<SelloEstado estado="SoloConsulta" />)
    expect(screen.getByText('Solo consulta')).toBeInTheDocument()
  })
})
