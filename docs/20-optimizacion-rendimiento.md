# Hito de optimización — rendimiento sin cambios de producto

Estado: implementación de los cuatro bloques comprobada en local el 4 de octubre de 2026.
Pendientes: publicación, primera ejecución de CI en GitHub y comprobación del despliegue.
La entrega se ha subido a GitHub (`7c1ac10`); no se ha confirmado todavía su despliegue en Render.

## Objetivo y orden

1. Reducir consultas repetidas del catálogo y carga de análisis históricos.
2. Extraer cuerpo, párrafos, título y referencias con una sola lectura del XML.
3. Separar responsabilidades del componente Angular y cancelar solicitudes obsoletas.
4. Automatizar pruebas, compilación y comprobación de migraciones en CI.

Se mantendrán los filtros, orden, enlaces oficiales, límites de cobertura y avisos de
incertidumbre. Los perfiles no se guardarán en el servidor y no se activará correo real.
El procesamiento por fragmentos de fuentes largas y la validación real de Gemini son
trabajos independientes, no se dan por resueltos con optimizaciones de consultas.

## Primera entrega: ventana de candidatos

Antes, la búsqueda personalizada recorría hasta diez páginas de 100 publicaciones.
Cada página repetía el recuento, la selección de documentos y la lectura de análisis:
hasta 30 consultas, más una para las revisiones oficiales almacenadas.

Ahora solicita una única ventana interna de hasta 1.000 señales. El listado público
sigue limitado a 100 documentos por página. Ambos recorridos reutilizan los mismos
filtros y orden del catálogo; no hay dos definiciones de señales de negocio.

La base de datos selecciona únicamente el análisis más reciente por documento, con
desempate por identificador. La búsqueda no descarga todas las versiones históricas
para elegir la primera en memoria, ni trae requisitos, plazos o evidencias que el
listado no utiliza. El detalle conserva su contrato completo y usa el mismo desempate.

Se conserva el orden personalizado antes de paginar, la instantánea temporal de las
revisiones y `isPartial` si existen más de 1.000 señales. Una búsqueda vacía no consulta
análisis ni revisiones.

## Verificación

Una prueba con PostgreSQL real crea 1.001 señales en un esquema aislado, añade dos
versiones de análisis a un documento y una revisión oficial. Comprueba:

- Cuatro consultas para obtener candidatos, análisis y revisiones, sin llamadas a BOE ni Gemini.
- Selección del análisis más reciente en SQL, sin columnas de requisitos, plazos o evidencias.
- Priorización de evidencia, paginación y mantenimiento de la cobertura parcial.
- Límite público de 100 resultados aunque el cliente solicite 1.000.
- Dos consultas cuando no hay señales y ninguna lectura de análisis o revisiones.

Otra prueba conserva exactamente los identificadores y el orden de una página interior
frente al algoritmo de ordenación anterior, sobre un conjunto de 1.001 publicaciones.
Las pruebas existentes siguen cubriendo filtros, límite de candidatos e instantáneas
de evidencia entre páginas.

La reducción de consultas es una medición directa. No se afirma una mejora concreta de
latencia o consumo de memoria: esa comparación requiere un benchmark representativo y
repetible. Tampoco se ha eliminado todavía la lectura de todo el historial de revisiones
oficiales; es una mejora posterior dentro de este bloque.

Para reproducir, ejecuta las pruebas con `BOERADAR_TEST_CONNECTION` apuntando a PostgreSQL
local en el puerto 54329, como se describe en las
[correcciones de fiabilidad](19-correcciones-fiabilidad.md).

Esta primera entrega no requiere migraciones nuevas ni cambios de configuración.
El hito no se da por publicado ni cerrado hasta verificar CI y el despliegue.

Resultado actualizado: 116 pruebas .NET, diez de Angular y ocho de reglas superadas,
sin omisiones. Las diez de Angular también pasan en Linux con Node 22.22, después de
una instalación limpia con `npm ci`; no se cuentan dos veces en el total de 134 casos.
La solución compila sin errores ni advertencias. La API, accedida a través del proxy de
la web local, responde HTTP 200 y conserva `Cache-Control: no-store`; dos páginas de
cuatro elementos mantienen la misma instantánea de evidencia y ocho identificadores
distintos sobre nueve señales. La API local vuelve a estar iniciada tras la verificación.

## Segunda entrega: extracción unificada del XML

`OfficialDocumentContentExtractor.ExtractDocument` devuelve cuerpo normalizado, SHA-256,
párrafos, título y referencias a partir de un único `XDocument`. La fuente utilizada por
la API y el análisis deja de analizar cuatro veces el mismo XML. Los métodos anteriores
siguen disponibles para consumidores que solo necesitan una parte del documento.

Las pruebas conservan una referencia fija del cuerpo de `BOE-A-2024-10761`: 23.468 caracteres
y hash `6fb111e35b63cb43e7e9ae92a85eec299e7c87fdc0d8feb48a03841e84329517`.
También contrastan párrafos, título y referencias, XML con espacios de nombres y ambos
nodos de texto, etiquetas anidadas, contenido vacío y XML mal formado. El comportamiento
de HTML no cambia. La consulta local de `BOE-A-2026-19942` devuelve revisión, hash de
64 caracteres, seis grupos de evidencia y una referencia oficial.

La reducción de cuatro análisis del XML a uno es estructural; no equivale a una medición
de latencia, memoria o rendimiento de extremo a extremo.

## Tercera entrega: estado del catálogo separado de la pantalla

`CatalogState` concentra búsquedas, selección de fichas y revisiones de fuente. Es una
instancia por pantalla, no un estado global. `App` mantiene perfil, preferencias y
suscripciones y delega las lecturas del catálogo. No cambian las plantillas ni los estilos.

Se cancelan las lecturas anteriores al buscar de nuevo, introducir un intervalo inválido,
abrir otra ficha, cerrar el detalle o destruir la pantalla. Se mantienen las guardas por
identificador de solicitud y se limpian explícitamente los indicadores de carga. Cambiar
el perfil o los filtros cierra una ficha del contexto anterior; evita mezclar su contraste
con el nuevo perfil. Si falla la revisión, el detalle oficial sigue disponible.

Las ocho pruebas del estado cubren estas transiciones y la instantánea entre páginas;
otras dos montan `App`, renderizan el catálogo y comprueban el cableado y su limpieza.
Las operaciones de alta, actualización y baja de suscripciones no se cancelan como si
fueran lecturas. No se ha activado ningún envío de correo ni guardado perfiles en servidor.

Desde `src/boe-radar-ui`:

```sh
npm ci
npm test
npm run build:production
npm audit --audit-level=high
```

La compilación de producción suma 279,68 kB iniciales, dentro del presupuesto actual.
Las dependencias añadidas son herramientas de prueba, no dependencias de la web publicada.
La instalación limpia en la imagen Linux informa de cero vulnerabilidades.

## Cuarta entrega: verificaciones automáticas

`.github/workflows/ci.yml` prepara tres trabajos para cambios en `main`, ramas `codex/**`,
pull requests a `main` y ejecución manual:

- Backend: restauración, compilación con advertencias como errores y pruebas .NET.
  PostgreSQL 17 temporal en `localhost:54329` permite ejecutar también las pruebas de
  persistencia. Cada caso usa su propio esquema. Una prueba comprueba que el modelo
  coincide con el snapshot y que no quedan migraciones pendientes después de migrar.
- Frontend: instalación limpia, diez pruebas, compilación de producción, auditoría de
  dependencias y ocho pruebas de las reglas de negocio.
- Imagen: construcción de `Dockerfile.render` después de pasar los otros dos trabajos.
  No publica imágenes, modifica Render ni utiliza secretos de producción.

El flujo solo necesita permiso de lectura del repositorio. Su configuración sigue las
acciones oficiales de [checkout](https://github.com/actions/checkout),
[setup-dotnet](https://github.com/actions/setup-dotnet) y
[setup-node](https://github.com/actions/setup-node).

En local se han comprobado las pruebas, el modelo/migraciones, compilaciones Windows y
Linux y la construcción de la imagen completa de Render. GitHub Actions todavía no se
ha completado con éxito: la primera ejecución pasó el backend y detectó que Node 22.22.0
era inferior al mínimo de Angular (22.22.3). Se ha fijado 22.22.3 en CI y en ambas imágenes
de la web; queda comprobar la ejecución corregida. No se ha configurado que Render
espere estos checks; la automatización de pruebas no constituye una barrera de despliegue
por sí sola. Tras publicar, hay que comprobar la primera ejecución y decidir esa política.

## Pendientes y límites

1. Revisar y publicar los cambios; comprobar la primera ejecución de GitHub Actions.
2. Desplegar en Render y verificar catálogo, perfil, páginas y fichas en producción.
3. Medir latencia y memoria con un conjunto representativo antes de afirmar una mejora
   cuantitativa; valorar limitar también la lectura del historial de revisiones.
4. Retomar validación real de Gemini, documentos largos, proveedor de correo y pruebas
   de utilidad con autónomos y pymes. Ninguno queda resuelto por este hito técnico.
