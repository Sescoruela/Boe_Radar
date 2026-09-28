# Validación inicial de relevancia para autónomos y pymes

Fecha: 26 de septiembre de 2026. Entorno: PostgreSQL y API locales, con las
ediciones oficiales del 24, 25 y 26 de septiembre (763 publicaciones), más
la edición histórica de demostración. Esta evaluación usa únicamente las
763 publicaciones recientes.

## Criterio de etiquetado

`Sí` significa que el texto ofrece una ayuda, cambio fiscal u obligación con
una posible acción directa para una empresa o profesional autónomo que pueda
acceder como nuevo solicitante, no solo para una lista de proyectos previamente
seleccionados. Esto no significa que cualquier negocio reúna los requisitos.
`No` significa que el
documento queda fuera de ese alcance (por ejemplo, convenio institucional,
edicto, contratación pública o subvención para personas/entidades distintas).
`Dudoso` exige revisar bases o anexos antes de juzgar una posible omisión.

La muestra es dirigida, **no aleatoria ni representativa**: incluye las tres
señales actuales, once casos próximos a las reglas y cuatro documentos por
sección 3, 4, 5A y 5B, elegidos con `ORDER BY md5(external_id)` dentro de
cada sección. Se inspeccionaron el texto oficial o los pasajes XML en 13 casos
prioritarios; los otros 17 se etiquetaron por título y sección. Por tanto,
estos resultados no estiman precisión ni recall globales.

| Grupo | IDs BOE | Etiqueta | Motivo principal |
| --- | --- | --- | --- |
| Mostrados | BOE-B-2026-30841, BOE-B-2026-30860, BOE-B-2026-30861 | Sí (3) | Auto+ admite empresas y personas físicas con actividad económica; Doctorados Industriales y Torres Quevedo nombran empresas beneficiarias. |
| Próximos: ayudas excluidas | BOE-B-2026-30833, BOE-B-2026-31264, BOE-B-2026-31265, BOE-B-2026-31266 | No (4) | Estudios o entidades de investigación/educación; no acción general para una pyme. |
| Próximos: otros ámbitos | BOE-A-2026-19845, BOE-A-2026-19941, BOE-A-2026-19997, BOE-A-2026-19988, BOE-B-2026-30929 | No (5) | Convalidación, retribución sectorial, convenios institucionales o concesión portuaria, fuera del radar de ayudas y obligaciones generales. |
| Casos revisados después | BOE-B-2026-30862, BOE-B-2026-31267 | No (1), Sí (1) | El primero financia solo proyectos internacionales ya seleccionados; Erasmus+ Deporte admite organizaciones deportivas privadas, incluidas potenciales pymes del sector. |
| Muestra sección 3 | BOE-A-2026-19935, BOE-A-2026-19940, BOE-A-2026-19986, BOE-A-2026-19989 | No (4) | Convenios institucionales. |
| Muestra sección 4 | BOE-B-2026-31067, BOE-B-2026-31090, BOE-B-2026-31091, BOE-B-2026-31115 | No (4) | Administración de Justicia, no aviso normativo o ayuda general. |
| Muestra sección 5A | BOE-B-2026-30782, BOE-B-2026-30831, BOE-B-2026-30894, BOE-B-2026-30906 | No (4) | Formalizaciones de contratación pública, fuera del alcance actual. |
| Muestra sección 5B | BOE-B-2026-30856, BOE-B-2026-31148, BOE-B-2026-31189, BOE-B-2026-31253 | No (4) | Expediente de aguas o anuncios de subastas, no ayuda general. |

Resultado revisado: 4 `Sí`, 26 `No`. Las tres señales mostradas originalmente
son `Sí`; Erasmus+ Deporte es un falso negativo de la regla previa. La
selección dirigida favorece resultados conocidos y no permite extrapolar
porcentajes de precisión o exhaustividad.

La [convocatoria de colaboración internacional](https://www.boe.es/diario_boe/txt.php?id=BOE-B-2026-30862)
y la [ficha de la Agencia Estatal de Investigación](https://aei.gob.es/convocatorias/buscador-convocatorias/proyectos-colaboracion-internacional-pci2026-2)
indican que se financian proyectos ya seleccionados en programas internacionales;
no es una oportunidad general para una pyme que descubra ahora el anuncio.
En cambio, el [extracto de Erasmus+ Deporte](https://www.boe.es/diario_boe/txt.php?id=BOE-B-2026-31267)
abre una ronda de solicitudes y la [Guía oficial Erasmus+ 2026](https://erasmus-plus.ec.europa.eu/programme-guide/part-b/key-action-1/sports-staff)
permite solicitantes públicos o privados que organicen deporte de base. Una
empresa deportiva podría encajar, sujeto a todos los demás requisitos; por
eso retiramos la exclusión automática de ese título.

## Calidad de la ficha

La [fuente oficial de Auto+](https://www.boe.es/diario_boe/txt.php?id=BOE-B-2026-30841)
enumera por separado empresas, personas físicas con actividad económica y
exclusiones. Tras la corrección, la ficha local muestra los tres pasajes de
«Destinatarios» en el mismo orden que el XML oficial; se verificó en la API y
en la web local. También conserva los tres importes de
presupuesto y el plazo, sin confundir el presupuesto total con la ayuda
individual. El canal de solicitud sigue sin identificarse en la ficha.

Las [ayudas de Doctorados Industriales](https://www.boe.es/diario_boe/txt.php?id=BOE-B-2026-30860)
y [Torres Quevedo](https://www.boe.es/diario_boe/txt.php?id=BOE-B-2026-30861)
incluyen empresas entre las entidades beneficiarias y remiten al artículo 14
de sus convocatorias para presentar solicitudes. La ficha localiza esa
remisión, pero todavía no convierte la referencia a la convocatoria completa
o la web de la Agencia Estatal de Investigación en un paso directo verificable.

## Siguiente puerta de calidad

1. Etiquetar al menos 200 publicaciones de varios días y medir por separado
   precisión y recall. El objetivo del MVP es ≥90 % de clasificación correcta
   en un conjunto etiquetado, no una promesa de certeza jurídica.
2. Repetir la evaluación en Render tras desplegar las reglas para confirmar
   que el comportamiento de producción coincide con el validado en local.
