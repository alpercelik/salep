'use strict';

// This hook is loaded by Mocha while the pinned graphql-js source test suite runs.
// It records inputs and outcomes only; it does not replace or copy reference parsing code.
const Module = require('node:module');
const fs = require('node:fs');
const originalLoad = Module._load;
const capture = { parseCases: [], helperCases: [], lexerCases: [], blockStringCases: [], excludedCases: [] };
const currentTest = Symbol.for('graphql-csharp-parser.reference-test');

function testTitle() {
  return globalThis[currentTest] ?? 'unknown upstream test';
}

Module._load = function (request, parent, isMain) {
  const exports = originalLoad.apply(this, arguments);
  const parentFile = parent?.filename ?? '';

  if (request === 'mocha' && parentFile.includes('/src/language/__tests__/')) {
    const wrapped = { ...exports };
    const originalIt = exports.it;
    wrapped.it = function (title, callback) {
      if (typeof callback !== 'function') return originalIt.apply(this, arguments);
      return originalIt.call(this, title, function (...args) {
        globalThis[currentTest] = this.test.fullTitle();
        capture.executedTests ??= [];
        capture.executedTests.push({
          test: this.test.fullTitle(),
          sourceFile: parentFile.split('/src/language/__tests__/')[1],
        });
        try {
          return callback.apply(this, args);
        } finally {
          globalThis[currentTest] = undefined;
        }
      });
    };
    return wrapped;
  }

  if (request === '../parser' && parentFile.includes('/src/language/__tests__/')) {
    const wrapped = { ...exports };
    for (const method of ['parse', 'parseValue', 'parseConstValue', 'parseType']) {
      const original = exports[method];
      if (typeof original !== 'function') continue;
      wrapped[method] = function (source, options) {
        const entry = {
          test: testTitle(),
          sourceFile: parentFile.split('/src/language/__tests__/')[1],
          method,
          source: typeof source === 'string' ? source : source?.body,
          options: options ?? null,
        };

        if (options && Object.keys(options).some((key) => !(method === 'parse' && key === 'maxTokens'))) {
          entry.excluded = 'Non-default graphql-js parser option: ' + Object.keys(options).join(', ');
          capture.excludedCases.push({ test: entry.test, sourceFile: entry.sourceFile, reason: entry.excluded });
          return original.apply(this, arguments);
        }

        try {
          const result = original.apply(this, arguments);
          entry.outcome = 'valid';
          capture[method === 'parse' ? 'parseCases' : 'helperCases'].push(entry);
          return result;
        } catch (error) {
          entry.outcome = 'invalid';
          entry.position = error.positions?.[0] ?? null;
          entry.message = error.message;
          capture[method === 'parse' ? 'parseCases' : 'helperCases'].push(entry);
          throw error;
        }
      };
    }

    if (typeof exports.parseSchemaCoordinate === 'function') {
      wrapped.parseSchemaCoordinate = function (source) {
        capture.excludedCases.push({
          test: testTitle(),
          sourceFile: parentFile.split('/src/language/__tests__/')[1],
          method: 'parseSchemaCoordinate',
          source,
          reason: 'parseSchemaCoordinate is a graphql-js helper API outside the GraphQL document grammar parser contract.',
        });
        return exports.parseSchemaCoordinate.apply(this, arguments);
      };
    }
    return wrapped;
  }

  if (request === '../lexer' && parentFile.endsWith('/src/language/__tests__/lexer-test.ts')) {
    const Lexer = exports.Lexer;
    const originalAdvance = Lexer.prototype.advance;
    const lexerStates = new WeakMap();
    Lexer.prototype.advance = function (...args) {
      let state = lexerStates.get(this);
      if (!state) {
        state = {
          test: testTitle(),
          sourceFile: 'lexer-test.ts',
          source: this.source.body,
          calls: 0,
          tokens: [],
        };
        lexerStates.set(this, state);
        capture.lexerCases.push(state);
      }

      state.calls++;
      try {
        const token = originalAdvance.apply(this, args);
        state.tokens.push({
          kind: token.kind,
          value: token.value,
          start: token.start,
          end: token.end,
        });
        return token;
      } catch (error) {
        state.error = {
          position: error.positions?.[0] ?? null,
          message: error.message,
        };
        throw error;
      }
    };
    return exports;
  }

  if (request === '../blockString' && parentFile.endsWith('/src/language/__tests__/blockString-test.ts')) {
    const wrapped = { ...exports };
    const original = exports.dedentBlockStringLines;
    wrapped.dedentBlockStringLines = function (lines) {
      const value = original.apply(this, arguments);
      capture.blockStringCases.push({
        test: testTitle(),
        sourceFile: 'blockString-test.ts',
        lines,
        value,
      });
      return value;
    };
    return wrapped;
  }

  return exports;
};

process.on('exit', () => {
  const output = process.env.GRAPHQL_REFERENCE_CAPTURE_PATH;
  if (output) fs.writeFileSync(output, JSON.stringify(capture, null, 2) + '\n');
});
