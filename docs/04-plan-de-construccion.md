# Plan de construcción

El objetivo es llegar pronto a una demo vertical real. Cada hito debe terminar
con software ejecutable, pruebas y documentación, no solo capas aisladas.

## Hito 0 — Validación técnica de la fuente

**Estado: completado el 21 de septiembre de 2026.**

**Resultado:** una utilidad local descarga un sumario real y muestra entradas
normalizadas.

- Guardar fixtures de tres días y varias secciones del BOE.
- Implementar el cliente HTTP y parser del sumario.
- Descargar HTML/XML, extraer texto canónico y calcular SHA-256.
- Probar IDs, URLs, caracteres, números extraordinarios y días sin edición.
- Documentar las condiciones de reutilización aplicables.

**Demo:** ejecutar una fecha por terminal y obtener un JSON normalizado.

Resultado verificado: el sumario de 29 de mayo de 2024 normaliza 216
documentos; la descarga de `BOE-A-2024-10761` extrae 23.468 caracteres de su
XML y produce un hash SHA-256 estable. La suite contiene 10 pruebas y tres
sumarios que cubren las secciones 1, 2A, 2B, 3, 4, 5A, 5B y 5C.

## Hito 1 — Esqueleto y catálogo sin IA

**Estado: completado el 22 de septiembre de 2026.**

**Resultado:** la web muestra publicaciones reales importadas en PostgreSQL.

- Crear solución .NET, proyectos, Angular y entorno local con PostgreSQL.
- Implementar entidades de fuente, migración inicial y casos de ingesta.
- Crear listado, detalle, filtros y paginación.
- Añadir logs estructurados, health checks y pruebas de integración.
- Construir y probar las imágenes de contenedor.

**Demo:** elegir una fecha, importar el BOE y navegar sus documentos.

Resultado verificado: la edición del 29 de mayo de 2024 se persiste con 216
documentos; una segunda ejecución deja los 216 sin cambios. La API ofrece
health checks, listado filtrable, paginación y detalle. La web Angular muestra
los 216 documentos, encuentra cuatro coincidencias para `ayudas` y conserva
en cada ficha los enlaces oficiales HTML, XML y PDF. La solución suma 13
pruebas automatizadas y la compilación de producción del frontend finaliza sin
errores.

## Hito 2 — Radar IA trazable

**Estado: implementación local completada el 22 de septiembre de 2026; validación
en Vertex AI pendiente de credenciales GCP.**

**Resultado:** los candidatos relevantes tienen un análisis verificable.

- Definir el esquema JSON y el prompt versionado.
- Implementar prefiltro determinista y adaptador de Vertex AI.
- Validar salida, fechas y evidencias; almacenar uso y versión del modelo.
- Preparar un golden set etiquetado y un comando de evaluación.
- Mostrar categorías, resumen, requisitos, plazos y confianza en la UI.

**Demo:** comparar documentos relevantes y descartados, abriendo la evidencia
de cada plazo.

Resultado local: el pipeline escanea 216 publicaciones, aplica un prefiltro,
descarga únicamente candidatos, valida citas literales y persiste el resultado
por hash, modelo y contrato `radar-v1`. La UI muestra categoría, resumen,
requisitos, plazos, evidencia y confianza. Existe un adaptador para Gemini con
salida JSON estructurada y un modo heurístico sin coste para desarrollo. El
golden set inicial contiene ocho casos etiquetados.

## Hito 3 — Suscripciones y alertas

**Estado: implementación y recorrido local verificados el 23 de septiembre de 2026 con PostgreSQL, Angular y Mailpit; proveedor real pendiente.**

**Resultado:** un usuario verifica su correo y recibe un digest idempotente.

- Alta, verificación, preferencias, gestión y baja.
- Matching por categoría y palabra clave.
- Outbox, plantilla de correo, reintentos y registro de entregas.
- Protección contra abuso y tratamiento de rebotes.

**Demo:** suscribirse, ejecutar una ingesta y generar un único digest por fecha y suscriptor.

La generación del digest es idempotente; la entrega SMTP es al menos una vez. Los rebotes del proveedor real quedan pendientes para producción. Véase [suscripciones y alertas](08-suscripciones-y-alertas.md).

## Hito 4 — Producción y portafolio

**Resultado:** aplicación pública reproducible y caso de estudio convincente.

- Infraestructura mínima con Terraform.
- Cloud Run, Cloud SQL, Scheduler, secretos, dominio y TLS.
- Dashboards y alertas operativas.
- CI para formato, pruebas, migraciones comprobadas e imágenes.
- Datos de demo, capturas, diagrama, decisiones y límites conocidos.
- README de instalación local, despliegue y recorrido de la demo.
- Caso de estudio con problema, decisiones, métricas, coste y aprendizajes.

## Orden inmediato de trabajo

1. Definir el contrato JSON versionado del análisis IA.
2. Crear un conjunto de documentos etiquetados para medir relevancia.
3. Implementar el prefiltro determinista antes de invocar el modelo.
4. Integrar Gemini mediante un puerto de aplicación reemplazable.
5. Mostrar resumen, requisitos, plazos, evidencia y confianza en la UI.

## Backlog posterior al MVP

- Perfil de empresa por CNAE, ubicación, tamaño y forma jurídica.
- BORME y boletines autonómicos.
- Alertas inmediatas para categorías críticas.
- Calendario y exportación iCalendar de plazos explícitos.
- Comparación de versiones y seguimiento de legislación consolidada.
- Búsqueda semántica y asistente con citas.
- Revisión editorial y feedback del usuario sobre relevancia.
- Equipos, roles y planes de pago.
