import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { safeAnchorId } from '../src/utils/anchorNavigation.ts';

test('anchor IDs preserve normal skip links and decode valid Unicode exactly once', () => {
  assert.equal(safeAnchorId('#main-content'), 'main-content');
  assert.equal(safeAnchorId('#lễ-cưới'), 'lễ-cưới');
  assert.equal(safeAnchorId(`#${encodeURIComponent('lễ cưới & bạn bè')}`), 'lễ cưới & bạn bè');
  assert.equal(safeAnchorId('#literal%2520percent'), 'literal%20percent');
  assert.equal(safeAnchorId('#a%2Fb%3Fc%23d'), 'a/b?c#d');
});

test('malformed percent encoding cannot throw during navigation', () => {
  for (const hash of ['#%', '#abc%2', '#%ZZ', '#%E0%A4%A', '#%ED%A0%80', '#%F0%28%8C%28', '#bad%FF']) {
    assert.doesNotThrow(() => safeAnchorId(hash), hash);
    assert.equal(safeAnchorId(hash), null, hash);
  }
  for (const hash of [undefined, null, '', '#', 'main-content']) assert.equal(safeAnchorId(hash), null);
});

test('layout effect uses guarded decoding and ID lookup without selector interpolation', () => {
  const source = readFileSync(new URL('../src/components/WeddingMatchLayout.tsx', import.meta.url), 'utf8');
  const effect = source.slice(source.indexOf('export default function WeddingMatchLayout'));
  assert.match(source, /import\s*\{\s*safeAnchorId\s*\}\s*from\s*["']\.\.\/utils\/anchorNavigation["']/);
  assert.match(effect, /const anchorId = safeAnchorId\(hash\)/);
  assert.match(effect, /if \(anchorId\)[\s\S]*?\.getElementById\(anchorId\)/);
  assert.match(effect, /else if \(!hash\) window\.scrollTo\(0, 0\)/);
  assert.doesNotMatch(effect, /decodeURIComponent|querySelector/);
});
