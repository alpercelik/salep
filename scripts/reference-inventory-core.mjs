import { createHash } from 'node:crypto';

export function stableTestIdentities(tests) {
  const occurrences = new Map();
  return tests.map((test) => {
    const identity = `${test.file}\0${test.fullTitle}`;
    const occurrence = (occurrences.get(identity) ?? 0) + 1;
    occurrences.set(identity, occurrence);
    const digest = createHash('sha256')
      .update(`${identity}\0${occurrence}`, 'utf8')
      .digest('hex')
      .slice(0, 20);
    return {
      id: digest,
      file: test.file,
      title: test.title,
      fullTitle: test.fullTitle,
      occurrence,
    };
  }).sort((left, right) => compareOrdinal(
    `${left.file}\0${left.fullTitle}\0${left.occurrence}`,
    `${right.file}\0${right.fullTitle}\0${right.occurrence}`,
  ));
}

export function compareInventory(expected, actual) {
  const expectedById = new Map(expected.tests.map((test) => [test.id, test]));
  const actualById = new Map(actual.tests.map((test) => [test.id, test]));
  const expectedSourceHashes = expected.provenance?.sourceSha256 ?? expected.sourceSha256;
  const actualSourceHashes = actual.provenance?.sourceSha256 ?? actual.sourceSha256;
  const added = [...actualById.keys()].filter((id) => !expectedById.has(id)).sort();
  const removed = [...expectedById.keys()].filter((id) => !actualById.has(id)).sort();
  const changedSources = Object.keys(expectedSourceHashes)
    .filter((file) => file in actualSourceHashes && expectedSourceHashes[file] !== actualSourceHashes[file])
    .sort();
  const addedSources = Object.keys(actualSourceHashes)
    .filter((file) => !(file in expectedSourceHashes))
    .sort();
  const removedSources = Object.keys(expectedSourceHashes)
    .filter((file) => !(file in actualSourceHashes))
    .sort();

  return { added, removed, changedSources, addedSources, removedSources };
}

export function hasInventoryChanges(changes) {
  return Object.values(changes).some((items) => items.length > 0);
}

export function compareSourcePaths(pinnedPaths, discoveredPaths) {
  const pinned = new Set(pinnedPaths);
  const discovered = new Set(discoveredPaths);
  return {
    added: [...discovered].filter((file) => !pinned.has(file)).sort(),
    removed: [...pinned].filter((file) => !discovered.has(file)).sort(),
  };
}

function compareOrdinal(left, right) {
  return left < right ? -1 : left > right ? 1 : 0;
}
