# ADR-001: monolito modular con worker separado

- Estado: aceptada
- Fecha: 2026-09-21

## Contexto

BOE Radar IA necesita servir una web, importar publicaciones una vez al día,
analizarlas con un proveedor externo y enviar alertas. Es un proyecto de una
sola persona y todavía no se conocen el volumen real, los límites de coste ni
las fronteras que cambiarán de forma independiente.

## Decisión

Mantener un único repositorio y una única aplicación lógica dividida en módulos
funcionales. Generar dos ejecutables: Web/API de larga duración y Worker de
ejecución finita. Ambos comparten bibliotecas de dominio, aplicación e
infraestructura y una base PostgreSQL.

Los módulos se comunican mediante contratos de aplicación. La entrega de
alertas usa una tabla outbox para que los cambios de negocio y la intención de
envío se confirmen atómicamente.

## Consecuencias positivas

- Menor coste de desarrollo, despliegue y observación.
- Transacciones locales e idempotencia más sencillas.
- Un flujo vertical completo puede construirse pronto.
- El worker puede escalar y ejecutarse con permisos distintos al proceso web.
- Los límites modulares dejan una ruta de extracción si aparece una razón real.

## Costes y límites

- Los módulos comparten despliegues de bibliotecas y esquema físico.
- Hay que proteger los límites con convenciones y pruebas de arquitectura.
- Un trabajo diario muy grande podría necesitar cola y paralelismo distribuido.
- Una migración defectuosa puede afectar a todas las funciones.

## Alternativas descartadas

### Microservicios desde el inicio

Añaden contratos remotos, observabilidad distribuida, despliegues coordinados y
fallos parciales sin una necesidad de escala demostrada.

### Solo funciones serverless

El pipeline tiene estado, reintentos por documento y procesamiento de duración
variable. Un Job contenedorizado ofrece un modelo más directo y sigue siendo
administrado.

### Un único proceso web con tareas en segundo plano

Cloud Run puede detener o escalar instancias y no es el lugar adecuado para un
lote diario largo. Un Job hace explícito el ciclo de vida y permite reejecución
por fecha.

## Señales para revisar esta decisión

- La ingesta supera de forma habitual su ventana operativa.
- Se necesitan reintentos independientes y gran paralelismo por documento.
- Un módulo requiere escalado, permisos o frecuencia de despliegue claramente
  diferentes.
- El equipo crece y las fronteras de propiedad se vuelven estables.
