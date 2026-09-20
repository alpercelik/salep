import assert from 'node:assert/strict';
import test from 'node:test';
import { compareInventory, compareSourcePaths, hasInventoryChanges, stableTestIdentities } from './reference-inventory-core.mjs';

test('test identities remain stable when input order changes', () => {
  const first = stableTestIdentities([
    { file: 'src/language/parser-test.ts', title: 'accepts', fullTitle: 'Parser accepts' },
    { file: 'src/language/lexer-test.ts', title: 'names', fullTitle: 'Lexer names' },
  ]);
  const reordered = stableTestIdentities([
    { file: 'src/language/lexer-test.ts', title: 'names', fullTitle: 'Lexer names' },
    { file: 'src/language/parser-test.ts', title: 'accepts', fullTitle: 'Parser accepts' },
  ]);

  assert.deepEqual(first, reordered);
});

test('duplicate full titles receive distinct stable occurrence identities', () => {
  const entries = [
    { file: 'src/language/parser-test.ts', title: 'accepts', fullTitle: 'Parser accepts' },
    { file: 'src/language/parser-test.ts', title: 'accepts', fullTitle: 'Parser accepts' },
  ];
  const identities = stableTestIdentities(entries);

  assert.equal(new Set(identities.map((entry) => entry.id)).size, 2);
  assert.deepEqual(identities.map((entry) => entry.occurrence), [1, 2]);
});

test('freshness comparison detects added, removed, renamed tests and changed files', () => {
  const expected = {
    tests: stableTestIdentities([
      { file: 'src/language/parser-test.ts', title: 'accepts', fullTitle: 'Parser accepts' },
      { file: 'src/language/lexer-test.ts', title: 'names', fullTitle: 'Lexer names' },
    ]),
    sourceSha256: {
      'src/language/parser-test.ts': 'before',
      'src/language/lexer-test.ts': 'stable',
    },
  };
  const actual = {
    tests: stableTestIdentities([
      { file: 'src/language/parser-test.ts', title: 'accepts', fullTitle: 'Parser accepts newer' },
      { file: 'src/language/printer-test.ts', title: 'prints', fullTitle: 'Printer prints' },
    ]),
    sourceSha256: {
      'src/language/parser-test.ts': 'after',
      'src/language/printer-test.ts': 'new',
    },
  };

  const changes = compareInventory(expected, actual);
  assert.equal(changes.added.length, 2);
  assert.equal(changes.removed.length, 2);
  assert.deepEqual(changes.changedSources, ['src/language/parser-test.ts']);
  assert.deepEqual(changes.addedSources, ['src/language/printer-test.ts']);
  assert.deepEqual(changes.removedSources, ['src/language/lexer-test.ts']);
  assert.equal(hasInventoryChanges(changes), true);
});

test('freshness comparison accepts an unchanged inventory', () => {
  const tests = stableTestIdentities([
    { file: 'src/language/parser-test.ts', title: 'accepts', fullTitle: 'Parser accepts' },
  ]);
  const inventory = { tests, sourceSha256: { 'src/language/parser-test.ts': 'digest' } };

  const changes = compareInventory(inventory, structuredClone(inventory));
  assert.equal(hasInventoryChanges(changes), false);
});

test('pinned source tree comparison rejects untracked additions and missing test files', () => {
  assert.deepEqual(
    compareSourcePaths(
      ['src/language/parser-test.ts', 'src/language/lexer-test.ts'],
      ['src/language/parser-test.ts', 'src/language/new-test.ts'],
    ),
    {
      added: ['src/language/new-test.ts'],
      removed: ['src/language/lexer-test.ts'],
    },
  );
});
