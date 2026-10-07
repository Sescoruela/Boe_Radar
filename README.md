# BOE Radar IA

BOE Radar IA explora publicaciones oficiales potencialmente relevantes para
autónomos y pymes. Muestra enlaces a la fuente y, al abrir una ficha, localiza
fragmentos del texto oficial para revisar destinatarios, requisitos, cuantías y
plazos. La selección y los fragmentos son automáticos y no verifican la
aplicabilidad a un negocio concreto.

## Estado

Los hitos **0 — validación técnica de la fuente** y **1 — catálogo sin IA**
están completados. El **Hito 2 — radar IA trazable** ya dispone de contrato,
prefiltro, validación, persistencia, integración con Gemini y presentación web;
la comprobación real en Vertex AI queda pendiente de credenciales GCP. El
**Hito 3 — suscripciones y alertas** dispone de flujo local, pruebas unitarias
y una demo integrada verificada con PostgreSQL y Mailpit. La conexión a un
proveedor de correo real y sus rebotes queda pendiente. La documentación base es:

- [Alcance del MVP](docs/01-mvp.md)
- [Arquitectura](docs/02-arquitectura.md)
- [Modelo de datos](docs/03-modelo-de-datos.md)
- [Plan de construcción](docs/04-plan-de-construccion.md)
- [Contrato y reutilización de la fuente BOE](docs/05-fuente-boe.md)
- [Desarrollo local](docs/06-desarrollo-local.md)
- [Radar IA trazable](docs/07-radar-ia.md)
- [Suscripciones y alertas](docs/08-suscripciones-y-alertas.md)
- [Despliegue de pruebas en Render](docs/09-despliegue-render.md)
- [Fichas accionables: primera iteración](docs/11-fichas-accionables.md)
- [Calidad de señales para negocios](docs/13-calidad-senales.md)
- [Personalización por negocio: primera iteración](docs/14-personalizacion.md)
- [Fichas adaptadas al tipo de publicación](docs/15-fichas-por-tipo.md)
- [Relevancia con evidencia: primera entrega](docs/16-relevancia-con-evidencia.md)
- [Orden personalizado y conservación de evidencia](docs/17-orden-con-evidencia.md)
- [Publicaciones relacionadas: referencias oficiales](docs/18-publicaciones-relacionadas.md)
- [Correcciones de fiabilidad y regresiones](docs/19-correcciones-fiabilidad.md)
- [Hito de optimización: rendimiento sin cambios de producto](docs/20-optimizacion-rendimiento.md)
- [Alertas personalizadas: primera entrega y requisitos de producción](docs/21-alertas-personalizadas.md)
- [ADR-001: monolito modular](docs/adr/001-monolito-modular.md)

## Propuesta de valor

> Cada mañana, saber qué ha publicado el BOE que puede afectar a tu negocio,
> por qué importa y qué fecha no debes perder, sin sustituir la lectura de la
> fuente oficial ni el asesoramiento profesional.

## Principios del producto

1. **La fuente manda.** Toda afirmación relevante debe poder rastrearse hasta
   el documento oficial.
2. **La IA extrae; la aplicación verifica.** La salida de Gemini usa un esquema
   estructurado y validaciones deterministas antes de publicarse.
3. **Incertidumbre visible.** Una confianza baja no se disfraza de certeza.
4. **MVP nacional y enfocado.** Primero BOE; después, si las métricas lo
   justifican, BORME y boletines autonómicos.
5. **Operación sencilla.** Un sistema que una sola persona pueda desplegar,
   observar y mantener.

## Stack propuesto

- ASP.NET Core y Entity Framework Core sobre .NET 10 LTS.
- Angular 22 con componentes standalone y signals.
- PostgreSQL para datos transaccionales y búsqueda de texto completo.
- Vertex AI con Gemini para clasificación y extracción estructurada.
- Cloud Run para API/web y Cloud Run Jobs para la ingesta diaria.
- Cloud SQL, Secret Manager, Cloud Scheduler y un proveedor de correo
  transaccional.

Las versiones exactas están fijadas en los archivos de proyecto y en los lock
files.

## Fuente primaria del MVP

La ingesta usa la API oficial de sumarios del BOE por fecha. Cada entrada del
sumario contiene enlaces a los formatos publicados (HTML, XML y PDF). La
aplicación conserva esos enlaces y trata el PDF firmado como referencia oficial
auténtica.

## Aviso

BOE Radar IA es una herramienta informativa. No ofrece asesoramiento jurídico,
fiscal o laboral. Ante cualquier discrepancia prevalece siempre la publicación
oficial.

## Hito 0: spike de la fuente

Requisitos: SDK de .NET 10.

```powershell
dotnet restore BoeRadar.slnx
dotnet test BoeRadar.slnx
dotnet run --project src/BoeRadar.SourceSpike -- --date 2024-05-29 --limit 1 --include-content
```

La utilidad consulta el sumario oficial, normaliza su estructura y emite JSON.
Con `--include-content` prefiere el XML del documento, extrae el cuerpo, lo
normaliza y calcula su SHA-256. Esta opción exige `--limit` para evitar una
descarga masiva accidental.

Fuente de los fixtures y datos de demostración: Agencia Estatal Boletín Oficial
del Estado. El producto derivado debe mostrar la atribución «Basado en datos de
la Agencia Estatal Boletín Oficial del Estado».

## Hito 1: catálogo ejecutable

Requisitos: SDK de .NET 10, Node.js 22.22.3 (o una versión compatible con Angular) y Docker Desktop.

```powershell
docker compose up -d postgres
dotnet run --project src/BoeRadar.Worker -- --date 2024-05-29 --migrate --trigger backfill
dotnet run --project src/BoeRadar.Web
```

En otra terminal:

```powershell
cd src/boe-radar-ui
npm ci
npm start
```

Para verificar la interfaz antes de publicar, desde el mismo directorio:

```powershell
npm test
npm run build:production
```

El [hito de optimización](docs/20-optimizacion-rendimiento.md) describe las pruebas de
PostgreSQL, las comprobaciones automáticas de GitHub y los límites de esta entrega.

El [hito de fichas orientadas a decisiones](docs/22-fichas-orientadas-a-decisiones.md)
describe los títulos abreviados, la cabecera de comprobaciones, los enlaces oficiales
y los estados de carga y reintento, sin confirmar elegibilidad ni plazos abiertos.

La [búsqueda por necesidades](docs/23-busqueda-por-necesidades.md) incorpora temas,
secciones avanzadas, fechas de publicación y alternativas para búsquedas vacías.

La aplicación queda disponible en `http://localhost:4200`. Consulta el
[manual de desarrollo local](docs/06-desarrollo-local.md) para ver el recorrido
de demostración, las pruebas y la configuración.
