# Personalización: primera iteración

Implementada y verificada en local el 3 de octubre de 2026. Publicada y
verificada en Render el 4 de octubre de 2026.

## Recorrido del usuario

1. Indicar tipo de negocio (autónomo o pyme), actividad y territorio.
2. Pulsar «Personalizar mi radar». La vista «Para mi negocio» prioriza las
   coincidencias y conserva las señales de alcance incierto.
3. Abrir la ficha para consultar los motivos y las comprobaciones pendientes,
   junto con los enlaces y fragmentos de la fuente ya existentes.
4. Cambiar el perfil, volver a las vistas generales o borrarlo.

No hace falta cuenta. El perfil se conserva en este navegador; no se sincroniza
entre dispositivos. Los filtros del catálogo siguen aplicándose. Sin fechas,
se mantiene la selección de los últimos 30 días.

## Arquitectura y datos

- Angular valida tres códigos contra listas permitidas. Guarda exclusivamente
  esos campos en `boe-radar-business-profile-v1`, con un sobre de versión 1.
  Un almacenamiento corrupto o inaccesible no impide explorar el catálogo.
  Si guardar falla, el perfil solo se aplica a la visita y se avisa al usuario.
- `POST /api/v1/publications/personalized` recibe `{ profile, search }`.
  El perfil contiene `businessType`, `activity` y `territory`; `search` utiliza
  los filtros, fechas y paginación existentes.
- La API valida los códigos y el orden de las fechas, responde con HTTP 400
  ante valores inválidos y marca la respuesta `Cache-Control: no-store`.
- `PersonalizedPublicationSearch` consulta únicamente señales de negocios.
  `BusinessProfileMatcher` puntúa título y epígrafe, nunca la dirección o el
  nombre del organismo emisor. No descarga documentos ni llama a Gemini.
- Se ordena antes de paginar: actividad +60, tipo de negocio explícito +20,
  mención territorial +10 o ámbito nacional/estatal explícito +5. Los empates
  se resuelven por fecha descendente e identificador oficial ascendente.
  No son porcentajes de confianza ni de elegibilidad y no se muestran como tales.
- Cada resultado añade `profileMatch`: prioridad, etiqueta, motivos y
  comprobaciones. La respuesta añade `catalogSignalCount` e `isPartial`.
- Máximo 1000 señales por consulta, en diez lotes de cien. Si hay más, se
  informa de cobertura parcial y se recomienda acotar las fechas. Las señales
  que no coinciden se mantienen dentro de esa ventana examinada.

No hay migración ni tabla de perfiles: el servidor recibe el perfil para
ordenar la consulta, pero no lo persiste. Se mantiene el esquema del catálogo.

## Verificación

- 12 casos nuevos de pruebas .NET: tildes, coincidencias explícitas,
  «organismos autónomos» no confundidos con trabajadores autónomos,
  territorio del emisor no usado como cobertura, incertidumbre visible,
  perfiles inválidos, orden entre páginas y límite de 1000 señales.
- Navegador conectado a Angular y API reales: creación, cambio de actividad,
  recarga con perfil conservado, apertura de ficha y borrado con nueva recarga.
- En el catálogo local de 1373 documentos y nueve señales, comercio/Baleares
  priorizó los precios de tabaco; formación/deporte priorizó Erasmus+.
  Ambas vistas conservaron las nueve señales.
- Las vistas «Todo el BOE» y «Señales para negocios» siguen accesibles.
- Peticiones HTTP con tipo inválido, perfil nulo y fechas invertidas: HTTP 400.
- Comprobación de ancho móvil: sin desbordamiento horizontal del documento.
- Suite completa: 49 pruebas .NET y ocho pruebas de los evaluadores aprobadas.
  Compilación Angular de producción correcta, sin advertencias de tamaño.
- Regresión de las tres ventanas congeladas: 267 publicaciones contrastadas,
  nueve positivos y 258 negativos correctos, sin errores en esta muestra
  dirigida. Esto no mide la calidad de la personalización por perfil.

## Límites y siguientes entregas

Las coincidencias son indicios de metadatos. Una mención territorial no confirma
cobertura y la ausencia de coincidencias no descarta relevancia. La actividad
es una selección amplia, no un CNAE; títulos sin términos reconocibles pueden
no recibir prioridad. La fuente oficial sigue siendo imprescindible.

Pendiente: validar con usuarios reales y una muestra etiquetada por perfiles;
adaptar las fichas por categoría y extraer vigencia; usar evidencia del texto
para mejorar la prioridad; agrupar cambios relacionados; conectar las alertas
al perfil cuando se habilite el correo. El perfil no determina beneficiarios,
obligaciones, plazos ni exclusiones.

## Publicación y validación en Render

Commit de aplicación: `32ad22a439793663da696792aec455bdd5464875`.
Despliegue manual: `dep-db1356gu01pc73ck489g`, con estado
`Deploy succeeded | Live`, duración 1m11s y fecha mostrada por Render
4 de octubre de 2026, 13:03:54 Europe/Madrid. No se añadieron secretos
ni migraciones. `/health/ready` devolvió HTTP 200 y base de datos conectada.

Tres perfiles sintéticos consultaron la misma ventana del 24 al 26 de
septiembre. Todos conservaron las seis señales y devolvieron `no-store`:

| Perfil | Primera señal esperada y observada |
| --- | --- |
| Autónomo, comercio, Baleares | BOE-A-2026-19942, precios de tabaco |
| Pyme, formación/deporte, sin territorio | BOE-B-2026-31267, Erasmus+ deporte |
| Pyme, transporte, Canarias | BOE-B-2026-30841, Auto+ |

En el navegador público se verificaron creación, recarga conservando el perfil,
ficha con motivos y comprobaciones, segunda página con señales inciertas,
cambio a pyme de formación y borrado confirmado mediante nueva recarga. La
portada mostró 3032 documentos y 19 señales; el cambio de actividad priorizó
las convocatorias de formación de ámbito estatal. Se borró el perfil sintético
al terminar para no dejarlo como preferencia del usuario.

Las tres ventanas congeladas también se evaluaron contra la API pública:
267 documentos revisados, nueve positivos y 258 negativos correctos, sin
errores observados. No es una evaluación global del catálogo ni de la
relevancia individual por negocio. Las pruebas de perfiles comprueban el
orden técnico; queda pendiente contrastar su utilidad con usuarios reales.

Se cierra la entrega de publicación y validación técnica de la primera
personalización. El siguiente hito es adaptar las fichas al tipo de publicación,
manteniendo explícitos los datos desconocidos y la evidencia oficial.
