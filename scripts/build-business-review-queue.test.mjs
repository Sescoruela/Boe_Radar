import assert from 'node:assert/strict';
import test from 'node:test';
import { buildReviewQueue } from './build-business-review-queue.mjs';

const publications = [
  { externalId: 'BOE-B-2026-1', publicationDate: '2026-09-24', sectionCode: '5B', title: 'Ayudas para empresas' },
  { externalId: 'BOE-B-2026-2', publicationDate: '2026-09-24', sectionCode: '5B', title: 'Subvenciones sectoriales' },
  { externalId: 'BOE-A-2026-3', publicationDate: '2026-09-25', sectionCode: '1', title: 'Orden administrativa' },
  { externalId: 'BOE-A-2026-4', publicationDate: '2026-09-26', sectionCode: '3', title: 'Convenio institucional' },
  { externalId: 'BOE-B-2026-5', publicationDate: '2026-09-26', sectionCode: '4', title: 'Edicto judicial' },
];

test('includes shown and near-miss cases before background', () => {
  const queue = buildReviewQueue(publications, ['BOE-B-2026-1'], 4);
  assert.deepEqual(queue.counts, { shown: 1, nearMiss: 1, background: 2 });
  assert.equal(queue.entries[0].externalId, 'BOE-B-2026-1');
  assert.equal(queue.entries[1].externalId, 'BOE-B-2026-2');
  assert.equal(new Set(queue.entries.map(item => item.externalId)).size, 4);
});

test('selection is independent of API pagination order', () => {
  const first = buildReviewQueue(publications, ['BOE-B-2026-1'], 5);
  const second = buildReviewQueue([...publications].reverse(), ['BOE-B-2026-1'], 5);
  assert.deepEqual(first, second);
});

test('rejects a signal missing from the catalog', () => {
  assert.throws(() => buildReviewQueue(publications, ['BOE-B-2026-99'], 3),
    /no figura/);
});
