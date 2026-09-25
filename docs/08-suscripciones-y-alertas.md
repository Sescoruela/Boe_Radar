# Hito 3 — Suscripciones y alertas

## Flujo

1. El usuario solicita alertas y acepta expresamente el envío de correos.
2. La API responde de forma genérica para no revelar si el correo ya existe. Un mensaje de verificación con enlace válido 24 horas queda en la outbox.
3. Al confirmar el enlace, se activa la suscripción y se envía por correo un enlace de gestión. La UI permite editar categorías, palabras clave y hora preferida, o darse de baja.
4. El worker busca análisis relevantes del día. Por defecto solo envía análisis `gemini`; el modo heurístico se admite únicamente con `--include-heuristic` para demos locales.
5. El matching exige coincidencia de categoría **y** de al menos una palabra clave cuando ambos filtros existen. Un filtro vacío admite todos los valores de ese tipo.
6. Cada digest incluye resumen, requisitos, plazos, enlace oficial y baja de un solo uso. Antes de enviar se comprueba que la suscripción siga activa.

## Ejecutar la demo local

Desde la raíz, con Docker Desktop, .NET 10 y Node.js compatible:

```powershell
docker compose up -d postgres mailpit
dotnet run --project src/BoeRadar.Worker -- --date 2024-05-29 --migrate --mode import --trigger backfill
dotnet run --project src/BoeRadar.Worker -- --date 2024-05-29 --mode analyze
dotnet run --project src/BoeRadar.Web
```

En otra terminal inicia Angular (`cd src/boe-radar-ui; npm start`). En `http://localhost:4200`, solicita las alertas. Luego ejecuta:

```powershell
dotnet run --project src/BoeRadar.Worker -- --mode dispatch
```

Abre `http://localhost:8025` para leer el correo de prueba; Mailpit no envía al exterior. Confirma el enlace y vuelve a ejecutar `--mode dispatch` para recibir el enlace de gestión. Finalmente:

```powershell
dotnet run --project src/BoeRadar.Worker -- --date 2024-05-29 --mode digest --include-heuristic
dotnet run --project src/BoeRadar.Worker -- --mode dispatch
```

La fecha histórica es intencionada: sirve para demostrar el flujo sin depender de publicaciones del día. En producción, programa la preparación del digest tras el análisis y la entrega por separado; para la fecha actual se respeta la hora de Madrid elegida por cada suscriptor.

## Garantías y límites

- Hay un digest único por suscriptor y fecha, matches únicos por análisis y claves únicas de outbox. Repetir la preparación no vuelve a crear el correo.
- La entrega usa reserva transaccional, reintentos con espera creciente y un máximo de cinco intentos. Un fallo entre la aceptación SMTP y el registro en base de datos aún podría producir un duplicado: la garantía externa es **al menos una vez**, no exactamente una vez.
- Los enlaces usan tokens aleatorios; en la base de datos solo se guardan hashes. Los cuerpos pendientes de envío contienen enlaces activos: protege el acceso a PostgreSQL y a Mailpit. Los cuerpos se vacían tras el envío o cancelación.
- El alta tiene un límite amplio por IP (100 solicitudes/hora), además del enfriamiento por correo pendiente. La verificación, gestión y baja no comparten ese cupo, para que una baja nunca dependa de las altas de otros usuarios tras un proxy. La respuesta de alta no revela si una dirección ya está registrada. Una baja invalida el enlace de gestión y se cancelan los digests pendientes al despachar.
- El estado `Bounced` está modelado, pero aún falta conectar webhooks de rebotes y supresión del proveedor real. Mailpit es solo para desarrollo; para producción se requiere SMTP transaccional con TLS, credenciales y gestión de rebotes. El recorrido local con PostgreSQL y Mailpit se verificó el 23 de septiembre de 2026; no se ha validado un envío real.

## API

- `POST /api/v1/subscriptions`: `{ email, categories, keywords, digestHour, consent }`. Las categorías usan nombres de enum (`Grant`, `Tax`, etc.).
- `POST /api/v1/subscriptions/verify`: `{ token }`.
- `GET /api/v1/subscriptions/me`: cabecera `X-Management-Token`.
- `PUT /api/v1/subscriptions/me`: preferencias y cabecera de gestión.
- `POST /api/v1/subscriptions/unsubscribe`: `{ token }` de gestión o baja.

No envíes tokens en parámetros de consulta ni en logs. La UI los recibe en el fragmento de URL y lo borra del historial al cargar.
