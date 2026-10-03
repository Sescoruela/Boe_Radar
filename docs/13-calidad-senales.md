# Hito de producto: señales fiables para negocios

Objetivo: que la vista inicial no oculte oportunidades reales para autónomos
y pymes ni presente anuncios ajenos como si fueran aplicables. Una señal es
una invitación a revisar la fuente, nunca una confirmación de elegibilidad.
El criterio de esta vista prioriza oportunidades a las que un negocio pueda
acceder como nuevo solicitante; las concesiones reservadas a proyectos ya
seleccionados no se tratan como convocatorias generales.

## Alcance y criterio

Esta revisión toma como señal una ayuda abierta a nuevos solicitantes o un
cambio fiscal u obligación general o sectorial que pueda afectar a autónomos
y pymes. Excluye contratación pública, subastas y expedientes dirigidos a
titulares individuales, aunque estos puedan ser importantes para sus partes.
La convalidación de una norma ya publicada no se cuenta como un cambio nuevo:
el radar debe analizar la disposición original y podrá enlazar posteriormente
el seguimiento parlamentario.
Los premios económicos quedan fuera del MVP por decisión del usuario;
podrán reconsiderarse en un hito posterior sin mezclarlos con subvenciones.

## Revisión de la muestra

Se revisaron los dos casos dudosos de la [validación inicial](12-validacion-relevancia.md)
con fuentes oficiales. La ayuda internacional `BOE-B-2026-30862` financia
proyectos ya seleccionados y no se incorpora como oportunidad general. La
convocatoria Erasmus+ Deporte `BOE-B-2026-31267` acepta organizaciones
deportivas privadas bajo condiciones específicas; se retira su exclusión
automática. Esta distinción evita tratar todas las ayudas de investigación o
deporte como equivalentes.

`data/business-signals-reviewed.json` guarda 200 etiquetas con motivo y
enlaces a las fuentes. El evaluador compara las etiquetas con ambas vistas
reales de la API y falla si falta algún documento del catálogo:

```powershell
node scripts/evaluate-business-signals.mjs http://127.0.0.1:5080
node scripts/evaluate-business-signals.mjs https://boe-radar-ia.onrender.com
```

Se congeló una cola de 200 publicaciones en
`data/business-signals-review-queue.json`. Contiene las 4 señales mostradas
cuando se congeló la cola,
51 posibles omisiones detectadas por palabras del título y 145 anuncios de
fondo repartidos por día y sección. La selección es reproducible mediante
una semilla y conserva una huella del catálogo de 763 documentos. La cola
por sí sola **no equivalía a 200 etiquetas**: el progreso se calcula contra el archivo de
decisiones revisadas, no se almacena dentro de la cola. Puede reproducirse
contra el mismo catálogo con otro nombre de archivo:

```powershell
node scripts/build-business-review-queue.mjs http://127.0.0.1:5080 200 data/otra-cola.json
```

El generador no sobrescribe una cola existente. Se recuperó el texto oficial
de las 182 publicaciones que faltaban y después se comprobó de nuevo la
accesibilidad de las 200 fuentes con
`scripts/fetch-business-review-sources.ps1` y se contrastó por grupos
documentales y casos de mayor riesgo. Las fuentes descargadas y sus huellas
quedan en `.tmp/business-review-sources.json` (caché local ignorada por Git).
Los scripts `review-structural-publications.mjs` y
`review-remaining-publications.mjs` comprueban el texto y su huella antes de
incorporar decisiones por tipo documental. Esta es una **revisión asistida**,
no dos anotaciones jurídicas independientes de cada publicación; conviene
auditar una segunda muestra antes de afirmar calidad en producción.

El conjunto revisado es **dirigido**, no representativo del BOE. Sus métricas sirven para
detectar regresiones concretas, no para prometer precisión o cobertura global.
En el catálogo local de 763 publicaciones de los días 24–26 de septiembre,
la primera regla mostró 3 señales y omitió Erasmus+ Deporte. Tras corregir
ese caso mostró 4. La revisión de 200 etiquetas encontró **2 falsos negativos**:
`BOE-A-2026-19943`, que
modifica los productos sujetos a inspección comercial en importación y
exportación, y `BOE-A-2026-19942`, que publica precios del tabaco aplicables
a estancos desde el mismo día. Antes de su corrección, la precisión era 100 %,
la exhaustividad (recall) 66,7 % y la exactitud global 99 %. Esta última
cifra quedaba inflada por la abundancia de negativos: el radar perdía 2 de
las 6 publicaciones relevantes de la muestra.
Las dos omisiones proceden del estrato de fondo, no de los 51 títulos
marcados como posibles omisiones por palabras clave; esto señala un límite
del filtro centrado en términos del título.

## Corrección y comprobación local

Se añadieron dos patrones estrechos en la sección I para inspección y control
de comercio exterior y para precios de venta de labores de tabaco en
expendedurías. En los 763 documentos del catálogo local solo incorporan los
dos casos confirmados. Una API de prueba compilada con .NET 10 devolvió 6
señales y, al evaluarlas contra las 200 etiquetas, obtuvo 6 verdaderos
positivos, 194 verdaderos negativos y ningún error. Las 37 pruebas .NET
pasaron. Es una comprobación de regresión sobre una muestra dirigida, no una
estimación de fiabilidad futura.

Las dos fichas respondieron con su revisión del XML oficial. La extracción
automática todavía no identifica en la ficha del tabaco su entrada en vigor
el mismo día, por lo que el usuario debe leer la fuente enlazada.

`BOE-A-2026-19845` se etiqueta como seguimiento de un real decreto-ley ya
publicado, no como cambio nuevo. Si se añaden alertas de seguimiento deberán
enlazar la norma original para no duplicar avisos.

## Puerta de cierre

### Segunda muestra: 23 de septiembre de 2026

Se importaron 151 publicaciones adicionales en local y se congeló una muestra
de 60 en `data/business-signals-review-queue-v2.json`, sin solapamiento de
fechas con la primera auditoría. Sus 60 fuentes oficiales fueron recuperadas.
Se registraron 60 negativos justificados en `data/business-signals-reviewed-v2.json`;
la API los descarta correctamente. No hay positivos en esta muestra:
precisión y recall son indeterminados, no 100 %. La muestra tampoco contiene
documentos de sección I y no valida la cobertura de cambios normativos.

Se resolvió el alcance de `BOE-A-2026-19752`: el Premio Nacional
de Arquitectura tiene dotación de 60.000 euros y contempla estudios de
arquitectura, pero las candidaturas se presentan mediante colegios y otras
entidades. El usuario decidió no incluir premios de momento: se etiqueta
como fuera de alcance, sin cambiar las reglas de producción.

```powershell
node scripts/evaluate-business-signals.mjs http://127.0.0.1:5080 data/business-signals-reviewed-v2.json data/business-signals-review-queue-v2.json
```

La segunda muestra está completa (60/60). Se amplió después con la revisión
normativa del día 22 y se publicó junto con las correcciones en GitHub.

### Ampliación normativa: 22 de septiembre de 2026

Se importaron otras 243 publicaciones. Antes de corregir las reglas, la vista
empresarial devolvía cero señales para esa fecha. La revisión dirigida de la sección I encontró dos
omisiones confirmadas por el texto oficial:

- [BOE-A-2026-19628](https://www.boe.es/diario_boe/txt.php?id=BOE-A-2026-19628):
  acuerdo de Seguridad Social España–Filipinas. Su artículo 4 regula
  certificados y comunicaciones de empleadores y trabajadores por cuenta
  propia desplazados. La entrada en vigor fue el 11 de septiembre, no la
  fecha de publicación del 22; no convertir publicación en fecha de efecto.
- [BOE-A-2026-19632](https://www.boe.es/diario_boe/txt.php?id=BOE-A-2026-19632):
  sentencia que anula, entre otros preceptos, la restricción para empresas
  de trabajo temporal en autorizaciones de residencia y trabajo de temporada.
  Es un cambio normativo sectorial aunque el título no mencione empresas.

No se ha etiquetado el catálogo completo del día 22 ni se han calculado
métricas sobre él. Estos hallazgos muestran que el filtro del título sigue
omitiendo señales de Seguridad Social y cambios normativos judiciales.
Se corrigieron mediante patrones de sección I para acuerdos administrativos
de convenios de Seguridad Social y sentencias estimatorias del Tribunal
Supremo sobre reglamentos de extranjería. No se añaden IDs particulares al
filtro. La segunda regla incluye también `BOE-A-2026-19633`, que reitera la
anulación del artículo 197.2: es una señal normativa relacionada y deberá
agruparse con 19632 si se generan alertas del mismo cambio.

Se congelaron y contrastaron las siete disposiciones generales del día 22
en los archivos `business-signals-review-queue-v3.json` y
`business-signals-reviewed-v3.json`. Las siete fuentes oficiales respondieron.
La API corregida muestra tres señales y descarta las otras cuatro; los dos
casos inicialmente omitidos quedan incluidos. Las otras muestras mantienen
6 positivos y 194 negativos correctos (200), y 60 negativos correctos (60).
Total revisado: 267 documentos, 9 positivos, 258 negativos, sin errores
observados; no representa una estimación global de cobertura. Pasaron las
37 pruebas .NET. La comprobación en Render se completó el 3 de octubre de 2026.

```powershell
node scripts/evaluate-business-signals.mjs http://127.0.0.1:5080 data/business-signals-reviewed-v3.json data/business-signals-review-queue-v3.json
```

1. ~~Etiquetar al menos 200 publicaciones~~: completado como revisión
   documental asistida; segunda muestra de 60 y revisión normativa de 7
   completadas, sin errores observados tras las correcciones.
2. ~~Corregir los dos falsos negativos y repetir precisión y recall en local~~:
   completado en la muestra, con 6/6 positivos detectados. El objetivo del
   MVP de al menos 90 % de clasificación correcta no basta por sí solo:
   solo hay 6 positivos, así que la exhaustividad medida es inestable.
3. ~~Repetir la evaluación en Render tras desplegar las reglas~~: completado
   el 3 de octubre de 2026. Las fichas enlazan las fuentes oficiales y explican
   los límites de la extracción automática.

El hito de corrección y validación de señales queda **cerrado para el MVP**,
con los límites de muestra y cobertura descritos en este documento.

## Comprobación pública del 3 de octubre de 2026

Se desplegó manualmente el commit `3b3c1a0febdc93a19e0d87badaf29f76e351f5a2`
en `boe-radar-ia`. Render confirmó `Deploy succeeded | Live` en el despliegue
`dep-db0inhugekts739sl6q0`. La versión anterior era `3325ee6`.

| Muestra | Revisadas | Positivos correctos | Negativos correctos | Errores |
| --- | ---: | ---: | ---: | ---: |
| 24–26 septiembre | 200 | 6 | 194 | 0 |
| 23 septiembre | 60 | 0 | 60 | 0 |
| Sección I, 22 septiembre | 7 | 3 | 4 | 0 |

Las huellas de los catálogos de las tres ventanas coincidieron con las colas
congeladas. La muestra sin positivos no permite medir precisión ni recall.
No se ha evaluado la relevancia de las publicaciones nuevas de octubre.

La web y `/health/ready` respondieron correctamente. La portada mostró
3032 publicaciones, cobertura hasta el 3 de octubre y 19 señales de los
últimos 30 días en el momento de la comprobación. Se probó la búsqueda
«Filipinas» y la apertura de `BOE-A-2026-19628`: fecha de publicación correcta,
enlaces HTML/XML/PDF oficiales y aviso de revisión obligatoria de la fuente.
Las revisiones de XML de 19628, 19632, 19942, 19943 y 31267 respondieron
correctamente con su huella y seis grupos de fragmentos.

### Mejoras del siguiente hito

- Adaptar la ficha al tipo de publicación: el acuerdo internacional todavía
  muestra apartados de convocatoria y fragmentos sobre prestaciones que no
  explican bien las obligaciones del artículo 4 para el negocio.
- Distinguir la fecha de publicación de la entrada en vigor y extraer esta
  última cuando el texto la indique; mientras tanto, se consulta en la fuente.
- Personalizar por actividad y territorio y agrupar cambios relacionados
  antes de emitir alertas. El correo público continúa desactivado.
