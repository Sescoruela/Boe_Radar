import { createHash } from 'node:crypto';
import { writeFileSync } from 'node:fs';
import { fetchPublications } from './evaluate-business-signals.mjs';
const window = { dateFrom: '2026-09-22', dateTo: '2026-09-22' };
const items = await fetchPublications(new URL(process.argv[2] ?? 'http://127.0.0.1:5080'), window, false);
const entries = items.filter(item => item.sectionCode === '1').map(item => ({
  externalId: item.externalId, publicationDate: item.publicationDate,
  sectionCode: item.sectionCode, title: item.title, stratum: 'normative',
  officialUrl: `https://www.boe.es/diario_boe/txt.php?id=${item.externalId}`,
}));
if (entries.length !== 7) throw new Error('La sección I no coincide con la revisión documentada.');
writeFileSync('data/business-signals-review-queue-v3.json', `${JSON.stringify({ ...window,
  catalogSha256: createHash('sha256').update(items.map(item => item.externalId).sort().join('\n')).digest('hex'),
  catalogCount: items.length, requestedCount: entries.length, entries }, null, 2)}\n`, { flag: 'wx' });
