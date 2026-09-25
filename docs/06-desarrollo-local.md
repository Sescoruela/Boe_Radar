# Desarrollo local

Este recorrido levanta PostgreSQL, importa una edición real del BOE y permite
explorarla desde la aplicación Angular.

## Requisitos

- SDK de .NET `10.0.401` (la versión esperada está en `global.json`).
- Node.js `22.22` o superior y npm.
- Docker Desktop con Docker Compose.

## Primera ejecución

Desde la raíz del repositorio:

```powershell
docker compose up -d postgres mailpit
dotnet restore BoeRadar.slnx
dotnet run --project src/BoeRadar.Worker -- --date 2024-05-29 --migrate --trigger backfill
dotnet run --project src/BoeRadar.Web
```

El worker aplica la migración con `--migrate` e importa la edición solicitada.
La operación es idempotente: repetirla no duplica ni modifica documentos cuyo
contenido no haya cambiado.

En una segunda terminal:

```powershell
cd src/boe-radar-ui
npm ci
npm start
```

Abre `http://localhost:4200`. El proxy de desarrollo dirige `/api` y `/health`
a la API en `http://localhost:5080`.
El buzón de prueba de alertas está en `http://localhost:8025`; consulta el
[recorrido del hito 3](08-suscripciones-y-alertas.md).

## Recorrido de demostración

1. Comprueba que el catálogo indica **216 resultados oficiales**.
2. Busca `ayudas`: la edición de demostración devuelve cuatro coincidencias.
3. Abre una ficha y comprueba fecha, sección, organismo y enlaces oficiales
   HTML, XML y PDF.
4. Repite la importación y verifica que el worker informa 216 documentos sin
   cambios y ninguno creado o actualizado.

## Endpoints

- `GET /health/live`: proceso activo.
- `GET /health/ready`: conexión con PostgreSQL disponible.
- `GET /api/v1/publications`: listado paginado con filtros `query`, `section`,
  `dateFrom` y `dateTo`.
- `GET /api/v1/publications/{id}`: detalle y enlaces a la fuente oficial.

## Verificación

```powershell
dotnet test BoeRadar.slnx --configuration Release
dotnet format BoeRadar.slnx --verify-no-changes
cd src/boe-radar-ui
npm run build
```

## Configuración

La conexión local predeterminada usa `localhost:54329`, base y usuario
`boeradar`, y la contraseña de desarrollo declarada en `docker-compose.yml`.
Para otro entorno, define `ConnectionStrings__BoeRadar` sin guardar secretos en
el repositorio.

## Parada

Detén la API y Angular con `Ctrl+C`. Para detener PostgreSQL conservando los
datos:

```powershell
docker compose stop postgres mailpit
```

## Ejecución en contenedores

Las imágenes de API, worker y web también se pueden construir y ejecutar con
Compose:

```powershell
docker compose --profile app build
docker compose --profile tools run --rm worker --date 2024-05-29 --migrate --trigger backfill
docker compose --profile app up -d
```

La UI queda en `http://localhost:4200` y reenvía las peticiones de la API por la
red interna de Compose.
