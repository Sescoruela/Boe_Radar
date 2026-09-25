# Radar IA trazable

El Hito 2 añade una canalización híbrida para detectar publicaciones relevantes
sin enviar todo el BOE al modelo.

## Flujo

1. Un prefiltro determinista puntúa título, organismo y epígrafe.
2. Solo los candidatos descargan el XML oficial.
3. El analizador produce JSON conforme al contrato `radar-v1`.
4. La aplicación valida confianza, fechas y citas antes de persistir.
5. El catálogo muestra el análisis junto con los enlaces oficiales.

La combinación de hash del contenido, modelo y versión del contrato hace que el
proceso sea idempotente y permite recalcular cuando cambia cualquiera de ellos.

## Contrato `radar-v1`

- `isRelevant`: relevancia para autónomos o pymes.
- `category`: `Grant`, `Subsidy`, `Tax`, `Obligation`, `Employment`,
  `Financing` u `Other`.
- `summary`: explicación breve en español.
- `requirements`: requisitos explícitos; lista vacía si no constan.
- `deadlines`: fecha ISO, descripción e indicador de que la fecha es explícita.
- `evidence`: cita literal y aspecto que respalda.
- `confidence`: valor entre 0 y 1.

Un resultado relevante sin evidencia se rechaza. Cada cita debe aparecer
literalmente en el texto normalizado del BOE y cada fecha explícita debe ser una
fecha ISO válida. La aplicación nunca calcula una fecha implícita como si fuera
oficial.

## Proveedores

### Heurístico

Es el valor local predeterminado. No usa IA ni tiene coste y sus resultados se
etiquetan con `method=heuristic` y una confianza conservadora del 55 %. Sirve
para desarrollar y demostrar el flujo completo, no para publicar alertas.

```json
"Analysis": {
  "Provider": "heuristic"
}
```

### Gemini en Google Cloud

La integración usa el SDK oficial `Google.GenAI`, autenticación Application
Default Credentials y salida JSON controlada por esquema.

```powershell
$env:Analysis__Provider = "gemini"
$env:Analysis__Gemini__Project = "mi-proyecto-gcp"
$env:Analysis__Gemini__Location = "europe-west1"
$env:Analysis__Gemini__Model = "gemini-3.5-flash"
gcloud auth application-default login
```

No se guarda ninguna credencial en el repositorio. El nombre del modelo es
configurable porque su ciclo de vida es externo a la aplicación.

## Ejecución

Analizar una edición ya importada:

```powershell
dotnet run --project src/BoeRadar.Worker -- \
  --date 2024-05-29 --migrate --mode analyze --limit 216
```

Importar y analizar en una sola ejecución:

```powershell
dotnet run --project src/BoeRadar.Worker -- \
  --date 2024-05-29 --migrate --mode pipeline --trigger scheduled
```

## Evaluación

`tests/BoeRadar.UnitTests/Fixtures/radar-golden-set.json` contiene los primeros
casos etiquetados positivos y negativos. La suite verifica el prefiltro, las
citas literales, las fechas explícitas y los límites de confianza. Este conjunto
es deliberadamente pequeño: debe crecer con falsos positivos y falsos negativos
observados antes de utilizar el radar para alertas.

## Límites actuales

- La demostración local no sustituye una evaluación del modelo en Vertex AI.
- El texto enviado al modelo se limita a 40.000 caracteres para controlar coste
  y latencia; los documentos truncados requieren una estrategia por fragmentos.
- La confianza es una señal del analizador, no una probabilidad calibrada.
- Toda decisión importante debe comprobarse en la publicación oficial.
