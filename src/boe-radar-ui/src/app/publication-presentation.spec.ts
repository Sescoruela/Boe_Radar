import { briefTitle, officialSource, publicationCategory } from './publication-presentation';
import { PublicationListItem } from './publications';

describe('Source-backed publication presentation', () => {
  const item: PublicationListItem = { id: 'a', externalId: 'BOE-B-2026-31900', title: 'Ayudas',
    publicationDate: '2026-10-01', sectionCode: '5B', sectionName: 'Otros anuncios', department: 'Ministerio' };

  it('removes the legal preamble but preserves the document type and full operative clause', () => {
    expect(briefTitle('Extracto de la Resolución de 30 de septiembre por la que se aprueba una convocatoria para entidades de formación.'))
      .toBe('Extracto: Se aprueba una convocatoria para entidades de formación.');
    expect(briefTitle('Orden de 1 de octubre por la que se modifican las bases, sin abrir el plazo.'))
      .toBe('Orden: Se modifican las bases, sin abrir el plazo.');
  });

  it('does not rewrite corrections, ambiguous titles or titles without the supported structure', () => {
    for (const title of ['Corrección de errores de la Orden por la que se convocan ayudas.', 'Ayudas a pymes',
      'Anuncio por el que se comunica una resolución']) expect(briefTitle(title)).toBe(title);
  });

  it('does not infer a category or eligibility from aid keywords', () => {
    expect(publicationCategory(item)).toBe('Pendiente de analizar');
    expect(publicationCategory({ ...item, analysis: { isRelevant: false, category: 'Grant', summary: '', confidence: 1, method: 'gemini' } }))
      .toBe('Pendiente de analizar');
  });

  it('offers a BOE link only for a valid official identifier', () => {
    expect(officialSource(item)).toBe('https://www.boe.es/diario_boe/txt.php?id=BOE-B-2026-31900');
    expect(officialSource(null)).toBeNull();
    expect(officialSource({ ...item, externalId: 'javascript:alert(1)' })).toBeNull();
  });
});
