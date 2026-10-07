import { PublicationListItem } from './publications';

export function categoryLabel(category: string): string {
  return ({ Grant: 'Ayudas', Subsidy: 'Subvenciones', Tax: 'Fiscalidad',
    Obligation: 'Obligaciones', Employment: 'Laboral', Financing: 'Financiación',
    Other: 'Otros temas' } as Record<string, string>)[category] ?? 'Publicación por revisar';
}

/** A shortened source title, not a generated summary. Preserve the document type. */
export function briefTitle(title: string): string {
  const original = title.replace(/\s+/g, ' ').trim();
  const clause = original.match(/\bpor (?:la|el) que\s+(.+)/i)?.[1];
  if (!clause) return original;
  const type = original.match(/^(Extracto|Resolución|Orden|Real Decreto|Decreto|Ley Orgánica|Ley|Acuerdo)\b/i)?.[1];
  if (!type) return original;
  return `${type}: ${clause.charAt(0).toLocaleUpperCase('es')}${clause.slice(1)}`;
}

export function publicationCategory(item: PublicationListItem): string {
  return item.analysis?.isRelevant ? categoryLabel(item.analysis.category) : 'Pendiente de analizar';
}

export function officialSource(item: PublicationListItem | null): string | null {
  return item && /^BOE-[AB]-\d{4}-\d+$/.test(item.externalId)
    ? `https://www.boe.es/diario_boe/txt.php?id=${encodeURIComponent(item.externalId)}` : null;
}
