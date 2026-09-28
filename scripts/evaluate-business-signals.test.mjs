import assert from 'node:assert/strict';
import test from 'node:test';
import { createHash } from 'node:crypto';
import { evaluateSignals, validateReviewQueue } from './evaluate-business-signals.mjs';

test('counts both kinds of mistakes and their IDs', () => {
  const labels = [
    { externalId: 'BOE-B-2026-1', relevant: true },
    { externalId: 'BOE-B-2026-2', relevant: true },
    { externalId: 'BOE-B-2026-3', relevant: false },
    { externalId: 'BOE-B-2026-4', relevant: false },
  ];
  const result = evaluateSignals(labels, labels.map(entry => entry.externalId),
    ['BOE-B-2026-1', 'BOE-B-2026-3']);

  assert.equal(result.truePositive, 1);
  assert.equal(result.falsePositive, 1);
  assert.equal(result.falseNegative, 1);
  assert.equal(result.trueNegative, 1);
  assert.equal(result.precision, 0.5);
  assert.equal(result.recall, 0.5);
  assert.deepEqual(result.falsePositiveIds, ['BOE-B-2026-3']);
  assert.deepEqual(result.falseNegativeIds, ['BOE-B-2026-2']);
});

test('rejects missing publications instead of counting them as negatives', () => {
  assert.throws(() => evaluateSignals(
    [{ externalId: 'BOE-B-2026-1', relevant: true }], [], []),
  /Falta en el catálogo/);
});

test('rejects duplicate labels', () => {
  const entry = { externalId: 'BOE-B-2026-1', relevant: true };
  assert.throws(() => evaluateSignals([entry, entry], [entry.externalId], []),
    /duplicada/);
});

test('rejects inconsistent catalog and signal snapshots', () => {
  assert.throws(() => evaluateSignals([], [], ['BOE-B-2026-1']),
    /no figura en el catálogo/);
});

test('keeps labels tied to the frozen review queue', () => {
  const ids = ['BOE-B-2026-1', 'BOE-B-2026-2'];
  const sample = { dateFrom: '2026-09-24', dateTo: '2026-09-26',
    labels: [{ externalId: ids[0], relevant: true, reason: 'Convocatoria abierta.',
      sources: [`https://www.boe.es/diario_boe/txt.php?id=${ids[0]}`] }] };
  const queue = { dateFrom: sample.dateFrom, dateTo: sample.dateTo,
    catalogSha256: createHash('sha256').update(ids.join('\n')).digest('hex'),
    requestedCount: 2, entries: ids.map(externalId => ({ externalId })) };

  assert.deepEqual(validateReviewQueue(queue, sample, ids),
    { queueCount: 2, queueReviewed: 1, queueRemaining: 1 });
  assert.throws(() => validateReviewQueue(queue, sample, [ids[0]]), /no corresponde/);
  assert.throws(() => validateReviewQueue(queue,
    { ...sample, labels: [{ ...sample.labels[0], sources: [] }] }, ids), /carece de motivo/);
});
