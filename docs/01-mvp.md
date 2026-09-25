# Alcance del MVP

## 1. Usuario y problema

### Usuario principal

Una persona autónoma o responsable de una pyme española que no puede revisar el
BOE a diario y quiere detectar novedades potencialmente relevantes sin depender
de una búsqueda manual.

### Trabajo que quiere resolver

> «Avísame cuando aparezca una ayuda, un cambio fiscal o una obligación que
> pueda afectarme; explícame en lenguaje claro por qué puede importarme, qué
> condiciones y fechas menciona, y llévame al texto oficial».

El MVP no intenta determinar con certeza jurídica si una empresa concreta puede
solicitar una ayuda o está obligada por una norma. Detecta y estructura señales
relevantes para que el usuario pueda comprobarlas.

## 2. Hipótesis que debe validar

1. El BOE contiene suficientes publicaciones accionables para justificar una
   revisión diaria enfocada en autónomos y pymes.
2. Un sistema híbrido (reglas más LLM) puede separar contenido relevante del
   ruido con una precisión útil.
3. Los usuarios valoran más la trazabilidad, los plazos y los requisitos que un
   resumen genérico.
4. Una alerta diaria agrupada es más útil y menos molesta que un correo por cada
   publicación.

## 3. Funcionalidad incluida

### Ingesta

- Consultar una vez al día el sumario oficial del BOE por fecha.
- Importar metadatos y obtener el contenido HTML o XML de cada documento.
- Conservar enlaces oficiales a HTML, XML y PDF.
- Evitar duplicados y permitir reejecutar una fecha sin crear datos repetidos.
- Registrar cada ejecución, sus errores y el estado de cada documento.

### Detección y enriquecimiento

- Prefiltrar mediante sección, departamento, título y palabras clave.
- Clasificar cada publicación como:
  - ayuda o subvención;
  - fiscal;
  - obligación o cumplimiento;
  - laboral o Seguridad Social;
  - contratación pública relevante;
  - no relevante.
- Extraer con respuesta estructurada:
  - resumen breve;
  - por qué puede ser relevante;
  - posibles destinatarios;
  - requisitos expresamente indicados;
  - plazos y fechas;
  - ámbito geográfico;
  - organismo emisor;
  - nivel de confianza;
  - fragmentos de evidencia para los datos críticos.
- Validar el esquema y las fechas antes de publicar el análisis.
- Marcar como «revisar en la fuente» los campos ambiguos o de baja confianza.

### Experiencia web

- Portada con las publicaciones relevantes más recientes.
- Búsqueda de texto y filtros por categoría, fecha, ámbito y organismo.
- Ficha de detalle con resumen, relevancia, requisitos, plazos, evidencias y
  enlaces oficiales.
- Estado de confianza visible y aviso de que no es asesoramiento profesional.
- Diseño responsive y accesible.

### Alertas

- Suscripción mediante correo verificado.
- Preferencias por categorías y palabras clave.
- Resumen diario con coincidencias nuevas y enlace a cada ficha.
- Enlace de baja con token de un solo uso.
- Registro idempotente del envío para no duplicar alertas.

### Operación

- Despliegue reproducible en GCP.
- Logs estructurados, health checks y métricas básicas de la ingesta.
- Página interna o endpoint protegido con el resultado de las últimas
  ejecuciones.
- Conjunto de documentos etiquetados para medir la calidad de clasificación y
  extracción.

## 4. Fuera del MVP

- BORME, diarios autonómicos, provinciales, municipales y fuentes europeas.
- Chat jurídico o respuestas libres sobre todo el corpus.
- Recomendaciones que afirmen elegibilidad u obligación legal definitiva.
- Integración con datos fiscales, contables o bancarios del usuario.
- Aplicación móvil nativa, notificaciones push, WhatsApp o Slack.
- Planes de pago, facturación y equipos multiusuario.
- Búsqueda vectorial, RAG conversacional y personalización por embeddings.
- Panel editorial completo y revisión humana de todas las publicaciones.
- Histórico exhaustivo anterior a la puesta en marcha.

## 5. Flujo esencial

1. El sistema importa el BOE del día.
2. Descarta candidatos claramente irrelevantes mediante reglas baratas.
3. Gemini devuelve una clasificación y extracción JSON para cada candidato.
4. El backend valida, guarda y publica los resultados aceptables.
5. El usuario consulta el radar o recibe un resumen diario según sus
   preferencias.
6. Desde cualquier dato crítico puede abrir la evidencia y el documento
   oficial.

## 6. Criterios de aceptación

El MVP se considera listo cuando:

- puede procesar y reprocesar 30 fechas consecutivas sin duplicados;
- una interrupción parcial puede reanudarse sin repetir alertas;
- al menos el 90 % de los documentos de un conjunto manualmente etiquetado se
  clasifican correctamente como relevante/no relevante;
- el 100 % de los plazos mostrados tiene texto de evidencia y enlace oficial;
- ningún documento se publica si la respuesta del modelo incumple el esquema;
- búsqueda y filtros responden en menos de 500 ms en el percentil 95 con el
  volumen previsto para un año;
- el correo diario incluye solo coincidencias no enviadas previamente;
- el despliegue y una demo completa se pueden reproducir siguiendo el README.

La métrica del 90 % es una meta inicial, no una promesa de exactitud jurídica.
Se medirá por separado la precisión y el recall para evitar que el sistema
parezca bueno simplemente descartando demasiado.

## 7. Riesgos que hay que probar pronto

| Riesgo | Experimento temprano | Mitigación |
|---|---|---|
| Demasiados falsos positivos | Etiquetar 200 entradas de varios días | Prefiltro por sección y umbrales por categoría |
| Plazos inventados o mal interpretados | Comparar extracción con 50 documentos | Evidencia obligatoria y validador de fechas |
| Coste o latencia de Gemini | Medir tokens por candidato | Limpiar texto, prefiltrar y cachear por hash |
| Cambios de formato del BOE | Fixtures reales de varias secciones | Adaptador de fuente y pruebas de contrato |
| Alertas molestas | Digest diario y preferencias simples | Límites, deduplicación y baja inmediata |
| Apariencia de asesoramiento legal | Revisión de copy y UI | Lenguaje probabilístico y fuente prominente |

## 8. Decisiones aplazadas

- Autenticación completa: para el MVP basta el correo verificado y un enlace
  seguro de gestión; las cuentas con contraseña pueden añadirse cuando exista
  una necesidad real.
- Proveedor de correo: se ocultará detrás de una interfaz y se elegirá al medir
  coste, entregabilidad y facilidad de uso.
- Histórico: se empezará con 30 días para validación y después se decidirá si
  merece la pena ampliar el backfill.
