# Despliegue de pruebas en Render

## Alcance

`render.yaml` crea una web Docker gratuita y PostgreSQL gratuito en Frankfurt.
El contenedor compila Angular y ASP.NET Core y sirve ambas partes bajo el mismo
dominio (`/` y `/api/v1`). La web aplica las migraciones al arrancar y Render
comprueba `/health/ready`. No se configura automáticamente ningún servicio de
pago ni se guardan secretos en el repositorio.

Este entorno **no es producción definitiva**: el servicio gratuito puede
dormirse por inactividad y la base de datos gratuita caduca a los 30 días, no
tiene copias de seguridad y admite hasta 1 GB. No guardes datos que necesites
conservar ahí. Consulta las condiciones actuales de Render antes de crearlo.

## Crear el entorno

1. Publica este proyecto en un repositorio Git privado o público en un
   proveedor admitido por Render. Este directorio local no está asociado
   actualmente a un repositorio Git; antes de desplegar faltará inicializarlo
   o conectar uno existente y subir los cambios. No incluyas `.env`, claves de
   GCP ni credenciales SMTP.
2. En Render, selecciona **New → Blueprint**, conecta el repositorio y revisa
   los recursos que propone `render.yaml` antes de confirmar. Comprueba que
   aparecen **una web Free** y **una base de datos Free** en Frankfurt.
3. Espera a que termine el despliegue y abre la URL HTTPS asignada. Prueba
   `/health/ready`, `/` y `/api/v1/publications`. La primera vez, la web
   importa el sumario del 24 de septiembre de 2026 si el catálogo está vacío.
   El ajuste `Bootstrap__InitialIssueDate` en `render.yaml` controla esa fecha;
   reiniciar la web no repite la carga cuando ya hay documentos.
4. No copies la URL interna de PostgreSQL a variables públicas o al navegador.
   El Blueprint la inyecta como `DATABASE_URL` solo en el servicio web.

`RENDER_EXTERNAL_URL` se usa para crear los enlaces de verificación y gestión
con el dominio real. Si luego usas un dominio propio como dirección principal,
configura `PublicBaseUrl` y ajusta la aplicación para darle preferencia sobre
`RENDER_EXTERNAL_URL` antes de cambiar los enlaces enviados.

## Ingesta, análisis y correo (opcional, con coste)

La web gratuita hace solo la carga inicial; **no ejecuta periódicamente** la
ingesta, el análisis, la preparación de digests ni la entrega de correo. Para probar el flujo completo
hay que crear tareas Cron Docker en Render con el mismo repositorio y
`src/BoeRadar.Worker/Dockerfile`. Cada tarea Cron tiene un cargo mínimo mensual
de 1 USD, además del uso; créalas solo si aceptas ese coste.

Configuración común de cada tarea: región `frankfurt`; `DATABASE_URL` con la
**Internal Database URL** de `boe-radar-db` como variable secreta;
`PublicBaseUrl` con la URL HTTPS de la web; `Analysis__Provider=heuristic`
para las primeras pruebas. La base de datos no admite conexiones externas con
el Blueprint actual, por lo que el cron debe usar la URL **interna** y estar en
la misma región. No actives Gemini hasta configurar las credenciales de Vertex
AI y revisar su coste.

| Tarea | Programación UTC sugerida | Docker Command |
| --- | --- | --- |
| Publicaciones y digest | `0 10 * * *` | `dotnet BoeRadar.Worker.dll --today --mode all --trigger scheduled` |
| Entrega de correo | `*/15 * * * *` | `dotnet BoeRadar.Worker.dll --mode dispatch` |

El primer comando ejecuta importación, análisis, preparación de digests y una
entrega. Como los digests heurísticos están desactivados por defecto, en esta
fase el análisis aparecerá en el catálogo pero **no** se enviarán digests de
prueba. Para una demostración controlada puedes añadir `--include-heuristic` al
comando diario; retíralo antes de usar destinatarios reales. `--today` calcula
la fecha de Europe/Madrid aunque el cron de Render se programe en UTC.

Para probar el alta, configura un proveedor SMTP transaccional: variables
`Email__Host`, `Email__Port`, `Email__From`, `Email__Username`,
`Email__Password` y `Email__EnableSsl=true` en la tarea de entrega. Render
bloquea los puertos SMTP 25, 465 y 587 desde las webs Free; verifica también
la conectividad real desde el cron y usa un puerto alternativo soportado por
tu proveedor si procede. No uses Mailpit ni `radar@localhost` en Render. Si
registras una suscripción y no recibes el enlace, revisa la ejecución y los
logs de la tarea de entrega; la API solo encola el mensaje.

## Comprobación y pendientes

- `GET /health/live` responde aunque PostgreSQL falle; `GET /health/ready`
  solo responde correctamente si PostgreSQL es accesible.
- Abre `/`, busca publicaciones y entra en un detalle. Refresca una ruta
  interna de Angular: debe volver a cargar la aplicación.
- Activa y dispara manualmente la tarea diaria desde Render para llenar el
  catálogo. Después prueba alta, verificación, gestión y baja con un correo
  propio; dispara la tarea de entrega cuando haya mensajes pendientes.
- Antes de datos reales faltan copias de seguridad, observabilidad, gestión de
  rebotes/supresión, límites de coste, pruebas con Gemini/Vertex AI y revisión
  de seguridad y privacidad. Las migraciones al inicio son adecuadas para esta
  única instancia de prueba, no para un despliegue con varias réplicas.

Fuentes: [Blueprints](https://render.com/docs/blueprint-spec),
[Free](https://render.com/docs/free),
[Cron jobs](https://render.com/docs/cronjobs).
