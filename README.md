# SSES: MVP del piloto (Sprints 1 a 3)

MVP del sistema de seguimiento de estrategias estudiantiles y del acompañamiento pedagógico, hecho para la demostración de la propuesta Mojave.

| Capa | Tecnología |
|---|---|
| API | .NET 10 LTS, Minimal API, EF Core 10 + Npgsql, JWT, ProblemDetails |
| Base de datos | PostgreSQL 17 |
| Web | React 19, TypeScript, Vite 8, Tailwind 4, TanStack Query 5 y aplicación web progresiva (funciona sin conexión) |
| Pruebas | xUnit + Testcontainers (API contra un PostgreSQL real) y Vitest + Testing Library |
| Despliegue | Docker Compose (db, api y web con nginx sin privilegios) |

## Cómo correrlo

Requisito: Docker Desktop encendido.

```bash
cp .env.example .env        # define DB_CLAVE y JWT_CLAVE (mínimo 32 caracteres)
docker compose up --build -d
```

Abre **http://localhost:8080**. Al arrancar, la API aplica las migraciones y carga los datos simulados del Ministerio: 6 entidades en 5 regiones, 24 docentes y 120 estudiantes.

Para apagarlo: `docker compose down`. Para borrar también los datos: `docker compose down -v`.

### Usuarios de demostración (clave `Piloto2026*`)

| Usuario | Perfil | Qué ve |
|---|---|---|
| `tutor.caribe` / `tutor.pacifico` | Tutor | Estrategias, estudiantes, acompañamientos |
| `tutor.bloqueo` | Tutor | Sirve para mostrar el bloqueo después de 5 intentos fallidos |
| `admin.mineducacion` | Administrador | Trazabilidad y exportación a CSV |
| `funcionario.men`, `ente.control`, `lider.piloto` | Otros perfiles | Aviso del módulo que llega en el Sprint 4 |

Estudiantes precargados: documentos `1100000001` a `1100000120` (tarjeta de identidad).

### Datos de demostración (`DATOS_DEMO=true`, activo por defecto en Compose)

Con la base vacía, la API carga una historia de uso de los últimos tres meses. Las fechas se calculan desde el día en que arranca, así que el mes en curso siempre tiene semanas registradas.

| Usuario | Estrategias |
|---|---|
| `tutor.caribe` | *Club de lectura en voz alta* (5 estudiantes, solo consulta, acompañamientos de septiembre a noviembre), *Semillero de robótica con material reciclado* (editable, 1 acompañamiento) y *Huerta escolar y matemáticas* (editable, sin acompañamientos) |
| `tutor.pacifico` | *Semillero de ciencia en el río Atrato* (4 estudiantes) y *Chirimía y memoria del Pacífico* (2 estudiantes) |
| `tutor.bloqueo` | *Bandas de paz* (2 estudiantes, acompañamiento de octubre) |
| `admin.mineducacion` | Cerca de 100 registros de trazabilidad: creación de estrategias, vínculos, seguimientos, ingresos de todos los perfiles, intentos fallidos y un bloqueo |

Para volver a este estado inicial: `docker compose down -v && docker compose up -d`. Con `DATOS_DEMO=false` solo se cargan los catálogos.

## Guion sugerido para la demostración (unos 5 minutos)

1. **CU-06:** ingresar con una clave errada (muestra los intentos restantes) y luego con la correcta.
2. **CU-01:** crear una estrategia. Primero intentar guardarla vacía para mostrar los errores por campo; después elegir una entidad del Pacífico y dos docentes.
3. **CU-02:** buscar `999` (no existe en la base precargada), luego `1100000004`, y vincular. El sello cambia a *Solo consulta*, el botón Editar desaparece y el documento se ve enmascarado (Ley 1581).
4. **CU-03:** registrar un acompañamiento individual y el seguimiento de la semana. Repetir con el mismo docente para mostrar el flujo 4A.
5. **RQ_21:** en las herramientas de desarrollo, activar *Offline*, registrar la semana siguiente (la barra se ve rayada en azul y aparece "1 por enviar"), volver a conectar y ver cómo se sincroniza sola.
6. **Trazabilidad:** ingresar como `admin.mineducacion`, filtrar por usuario y descargar el CSV.

En `capturas/` están las pantallas de este recorrido en celular (`m*`) y en escritorio (`d*`).

## Desarrollo local

```bash
# API (necesita un PostgreSQL; las pruebas levantan uno propio con Testcontainers)
cd backend && dotnet test

# Web: el proxy de Vite envía /api a http://localhost:8080
cd web && npm install && npm run dev
npm test          # pruebas
npm run build     # verificación de tipos y build de producción
```

## Trazabilidad de requerimientos a pruebas

| Requerimiento | Regla | Pruebas |
|---|---|---|
| CU-06 / RQ_13 | Acceso por perfil, bloqueo temporal tras 5 intentos, auditoría de ingresos | `AccesoTests`, `UsuarioTests`, `Login.test`, `App.test` |
| CU-06 pasos 4-5, 5A | Consulta de trazabilidad con filtros, mensaje sin resultados, CSV | `AccesoTests`, `Trazabilidad.test` |
| CU-01, 2A | Crear y editar con datos obligatorios; docentes de la misma entidad | `EstrategiaTests`, `EstrategiasTests`, `Estrategias.test` |
| RQ_03 | Con estudiantes vinculados, la estrategia queda solo para consulta | `EstrategiaTests`, `EstrategiasTests`, `Estrategias.test`, `EstrategiaDetalle.test` |
| CU-02 | Solo estudiantes precargados, sin duplicados; documento enmascarado | `EstrategiasTests`, `EstrategiaDetalle.test` |
| CU-03, 4A | Uno por docente o grupo al mes; el grupal exige 2 o más docentes | `AcompanamientoTests`, `AcompanamientosTests`, `Acompanamientos.test` |
| CU-03, 5A | Seguimiento semanal (semanas 1 a 5, avance de 0 a 100); actualizar la misma semana | `AcompanamientoTests`, `AcompanamientosTests`, `componentes.test` |
| HU-07 | Historial por docente | `AcompanamientosTests`, `Acompanamientos.test` |
| RQ_21, 6A | Registro sin conexión, cola en IndexedDB, reenvío idempotente con Id del cliente | `AcompanamientosTests`, `colaOffline.test`, `Acompanamientos.test` |

Estado verificado el 18/11/2026: API 58/58, web 50/50 y recorrido E2E con Playwright contra el stack de Docker Compose (todos los flujos anteriores, incluida la desconexión real del navegador).

## Seguridad

- Contraseñas con PBKDF2 (`PasswordHasher`), JWT de 8 horas, límite de 30 intentos de ingreso por minuto por IP y bloqueo por cuenta.
- Autorización por política en cada endpoint: un tutor solo ve sus propias estrategias.
- nginx con CSP estricta, `X-Frame-Options: DENY` y `nosniff`; los contenedores corren sin root.
- Los secretos van en `.env`, que no se versiona.
