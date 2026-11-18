export type Rol = 'Tutor' | 'Funcionario' | 'EnteControl' | 'LiderPilotaje' | 'Administrador'
export type EstadoEstrategia = 'Editable' | 'SoloConsulta'
export type TipoAcompanamiento = 'Individual' | 'Grupal'

export interface Usuario {
  id: number
  username: string
  nombre: string
  rol: Rol
  region: string
}

export interface Sesion {
  token: string
  expira: string
  usuario: Usuario
}

export interface Entidad {
  id: number
  nombre: string
  region: string
  municipio: string
}

export interface Docente {
  id: number
  documento: string
  nombre: string
}

export interface Estudiante {
  id: number
  tipoDocumento: string
  numeroDocumento: string
  nombre: string
  vinculadoEn: string | null
}

export interface EstrategiaResumen {
  id: number
  nombre: string
  periodo: string
  entidad: string
  region: string
  estado: EstadoEstrategia
  totalEstudiantes: number
  totalDocentes: number
  totalAcompanamientos: number
  actualizadaEn: string
}

export interface Estrategia {
  id: number
  nombre: string
  descripcion: string
  periodo: string
  entidad: Entidad
  estado: EstadoEstrategia
  esEditable: boolean
  docentes: Docente[]
  estudiantes: Estudiante[]
}

export interface Seguimiento {
  id: string
  semana: number
  actividades: string
  avance: number
  observaciones: string
  actualizadoEn: string
  pendiente?: boolean
}

export interface Acompanamiento {
  id: string
  estrategiaId: number
  tipo: TipoAcompanamiento
  periodo: string
  objetivo: string
  compromisos: string
  docentes: Docente[]
  avanceActual: number
  seguimientos: Seguimiento[]
  registradoEn: string
  existente: boolean
  pendiente?: boolean
}

export interface RegistroAuditoria {
  id: number
  usuario: string
  operacion: string
  recurso: string
  recursoId: string | null
  detalle: string | null
  fecha: string
}

export const nombreRol: Record<Rol, string> = {
  Tutor: 'Tutor',
  Funcionario: 'Funcionario del Ministerio',
  EnteControl: 'Ente de control',
  LiderPilotaje: 'Líder de pilotaje',
  Administrador: 'Administrador',
}
