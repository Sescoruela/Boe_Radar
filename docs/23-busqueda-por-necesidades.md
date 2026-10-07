# Hito de producto 2: buscar por necesidades del negocio

## Alcance

Filtros por ayudas/subvenciones, fiscalidad y obligaciones/cambios normativos.
Las secciones oficiales del BOE pasan a un bloque avanzado. Las fechas se
identifican como fechas de publicación, no de solicitud o entrada en vigor.
No requiere migración de datos, modelo externo, cuenta ni activar correos.

## Contrato de búsqueda

`PublicationSearch.Intent` es opcional: `grants`, `tax`, `obligations`, vacío o nulo.
GET `/api/v1/publications` admite `intent`; POST `/api/v1/publications/personalized`
lo admite dentro de `search`. Otros valores devuelven un error de validación.
El resto de filtros se combina mediante AND. Omitir el tema conserva la búsqueda anterior.

La selección temática se basa en menciones del título o epígrafe, mediante una
expresión regular traducida a PostgreSQL. El organismo emisor no determina el tema.
No es la categoría de un análisis IA ni acredita relevancia, elegibilidad o vigencia.
Los temas pueden solaparse y las menciones negativas pueden aparecer: se indica
que hay que revisar el contexto. Se evita el falso positivo conocido del nombre
«Ayuda al Refugiado» como si fuese una ayuda económica.

Se filtra **antes** de contar, paginar y obtener la ventana personalizada de hasta
1.000 candidatos. Se mantienen los límites existentes y la revisión de evidencia
guardada. No hay llamadas a BOE o Gemini durante el filtrado.

## Recorrido del cliente

1. Elegir qué revisar, opcionalmente un término y fechas de publicación.
2. Abrir filtros avanzados solo si necesita una sección oficial.
3. Pulsar Explorar radar. Los resultados muestran el tema realmente aplicado.
4. Editar el formulario no cambia la etiqueta de los resultados existentes.
   Paginar mantiene la búsqueda aplicada y su instantánea de evidencia.
5. Los motivos del radar distinguen menciones y aplicabilidad pendiente.
   Sin coincidencias claras no se elimina una señal que pase los filtros elegidos.
6. Una búsqueda vacía no significa que no existan oportunidades. Se ofrecen
   «Buscar en todos los temas» (conserva los otros filtros aplicados y el perfil)
   y «Ver catálogo sin filtros» (quita filtros y sale de la vista personalizada,
   pero no borra el perfil guardado).

## Verificación

- PostgreSQL real: recuentos y dos páginas de ayudas, combinación de término,
  fechas y sección, temas, falso positivo de asociación, ranking personalizado
  después de filtrar y rechazo de valores desconocidos.
- Angular: contratos GET/POST, formulario separado de filtros aplicados,
  paginación sin incorporar cambios no enviados y recuperación desde resultados vacíos.
- Compilación de producción y recorrido local en navegador.

Resultado local, 4 de octubre de 2026: 137 pruebas .NET y 28 Angular aprobadas,
sin omisiones, con PostgreSQL y el buzón Mailpit exclusivamente locales.
Se comprobó en navegador: ayudas con cuatro resultados (sin la sentencia que
menciona la asociación), fiscalidad sin resultados en esta selección local y
ampliación a todos los temas recuperando nueve señales sin borrar el perfil.
También se revisó el desplegable de secciones y se verificó HTTP 400 para un tema inválido.

No se evalúa aquí exhaustividad con usuarios ni se confirma que los requisitos
de las publicaciones se cumplan. La selección temática necesita validación con
un corpus etiquetado más amplio; la clasificación de fases y plazos pertenece
al siguiente hito. La revisión visual móvil sigue pendiente.

Los cambios permanecen locales hasta solicitar su publicación en GitHub/Render.
