# Hito: primeras señales útiles para negocios

## Qué ofrece esta iteración

- La portada muestra primero **señales para negocios**, no todas las publicaciones.
  Es una selección orientativa basada en el título y la sección del BOE:
  ayudas y subvenciones en V.B, y posibles cambios fiscales o empresariales en I.
- La vista **Todo el BOE** mantiene el catálogo completo y los filtros.
- La portada enseña la fecha más reciente presente en la base de datos y el
  número total de publicaciones. No afirma que la ingesta sea diaria.
- Con `Ingestion__RefreshEnabled=true`, la web comprueba los siete días más
  recientes al despertar y cada seis horas mientras siga activa. Reintenta
  fechas sin publicaciones, pero no equivale a un cron garantizado.
- Si un documento aún no tiene análisis, su ficha indica exactamente qué debe
  comprobarse en la fuente oficial. No inventa beneficiarios, importes ni plazos.
- El formulario de alertas solo aparece cuando `Features__EmailAlertsEnabled=true`.
  La API de alta devuelve 503 mientras la función no esté operativa. No se debe
  activar esa variable hasta probar la entrega de correo de principio a fin.

## Limitaciones deliberadas

La selección automática puede dar falsos positivos y negativos. Todavía no
equivale a una recomendación personalizada ni verifica que el usuario sea
beneficiario. La base inicial contiene el sumario del 24-09-2026 y el refresco
depende de que la web gratuita esté despierta. Un cron garantizado y el
enriquecimiento de fichas quedan para la siguiente iteración. Los documentos ya analizados conservan su resumen, requisitos,
plazos y evidencias en el detalle.

## Criterios de comprobación

1. `GET /api/v1/catalog/status` devuelve fecha y total del catálogo, además
   del estado real de las alertas.
2. `GET /api/v1/publications?businessSignalsOnly=true` excluye nombramientos
   y la ayuda al estudio del ejemplo inicial; la vista completa sigue
   devolviendo todos los documentos.
3. La web indica la fecha de cobertura, ofrece ambas vistas y no invita a
   dejar un correo cuando las alertas están desactivadas.
4. Una ficha sin análisis muestra una guía de verificación y enlaces oficiales.
