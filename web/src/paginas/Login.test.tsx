import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ProveedorSesion } from '../lib/sesion'
import { Login } from './Login'

// CU-06 flujo principal 1-2 y alterno 1A.

const responder = (status: number, cuerpo: unknown) =>
  vi.spyOn(globalThis, 'fetch').mockResolvedValue(
    new Response(JSON.stringify(cuerpo), { status, headers: { 'Content-Type': 'application/json' } }),
  )

const pintar = () =>
  render(
    <MemoryRouter>
      <ProveedorSesion>
        <Login />
      </ProveedorSesion>
    </MemoryRouter>,
  )

async function ingresar(usuario = 'tutor.caribe', clave = 'mala') {
  await userEvent.type(screen.getByLabelText(/usuario/i), usuario)
  await userEvent.type(screen.getByLabelText(/contraseña/i), clave)
  await userEvent.click(screen.getByRole('button', { name: /ingresar/i }))
}

describe('Login', () => {
  afterEach(() => vi.restoreAllMocks())

  it('con credenciales incorrectas dice cuántos intentos quedan', async () => {
    responder(401, { codigo: 'credenciales_invalidas', detail: 'El usuario o la contraseña no son correctos.', intentosRestantes: 3 })
    pintar()

    await ingresar()

    expect(await screen.findByRole('alert')).toHaveTextContent(/te quedan 3 intentos/i)
  })

  it('al quinto intento informa hasta qué hora está bloqueada la cuenta', async () => {
    responder(423, { codigo: 'cuenta_bloqueada', detail: 'Bloqueada', bloqueadoHasta: '2026-11-18T15:30:00-05:00' })
    pintar()

    await ingresar()

    expect(await screen.findByRole('alert')).toHaveTextContent(/bloqueada/i)
    expect(screen.getByRole('alert')).toHaveTextContent(/\d{1,2}:\d{2}/)
  })

  it('con credenciales válidas guarda la sesión', async () => {
    responder(200, {
      token: 'jwt',
      expira: '2099-01-01T00:00:00Z',
      usuario: { id: 1, username: 'tutor.caribe', nombre: 'Laura Gómez', rol: 'Tutor', region: 'Caribe' },
    })
    pintar()

    await ingresar('tutor.caribe', 'Piloto2026*')

    await vi.waitFor(() => expect(localStorage.getItem('sses.sesion')).toContain('jwt'))
  })

  it('no envía el formulario vacío', async () => {
    const fetch = vi.spyOn(globalThis, 'fetch')
    pintar()

    await userEvent.click(screen.getByRole('button', { name: /ingresar/i }))

    expect(fetch).not.toHaveBeenCalled()
  })
})
