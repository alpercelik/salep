'use strict';

// This hook is loaded by Mocha while the pinned graphql-js source test suite runs.
// It records inputs and outcomes only; it does not replace or copy reference parsing code.
const Module = require('node:module');
const fs = require('node:fs');
const originalLoad = Module._load;
const capture = { parseCases: [], helperCases: [], lexerCases: [], blockStringCases: [], utilityCases: [], schemaCoordinateCases: [], printCases: [], locationPrintCases: [], sourceCases: [], predicateCases: [], coordinateLexerCases: [], visitorCases: [], excludedCases: [] };
const currentTest = Symbol.for('graphql-csharp-parser.reference-test');
const parsedSources = new WeakMap();

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

        const supportedOptions = method === 'parse' ? new Set(['maxTokens', 'noLocation', 'allowLegacyFragmentVariables']) : new Set();
        if (options && Object.keys(options).some((key) => !supportedOptions.has(key))) {
          entry.excluded = 'Non-default graphql-js parser option: ' + Object.keys(options).join(', ');
          capture.excludedCases.push({ test: entry.test, sourceFile: entry.sourceFile, reason: entry.excluded });
          return original.apply(this, arguments);
        }

        try {
          const result = original.apply(this, arguments);
          entry.outcome = 'valid';
          capture[method === 'parse' ? 'parseCases' : 'helperCases'].push(entry);
          if (method === 'parse' && result && typeof result === 'object') parsedSources.set(result, { source: entry.source, options: options ?? null });
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
        const entry = {
          test: testTitle(),
          sourceFile: parentFile.split('/src/language/__tests__/')[1],
          source: typeof source === 'string' ? source : source?.body,
        };
        try {
          const result = exports.parseSchemaCoordinate.apply(this, arguments);
          capture.schemaCoordinateCases.push({ ...entry, expected: { outcome: 'valid', ast: canonicalNode(result) } });
          return result;
        } catch (error) {
          capture.schemaCoordinateCases.push({ ...entry, expected: { outcome: 'invalid', position: error.positions?.[0] ?? null } });
          throw error;
        }
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
      capture.utilityCases.push({ test: testTitle(), sourceFile: 'blockString-test.ts', method: 'dedentBlockStringLines', input: lines, expected: value });
      capture.blockStringCases.push({
        test: testTitle(),
        sourceFile: 'blockString-test.ts',
        lines,
        value,
      });
      return value;
    };
    for (const method of ['isPrintableAsBlockString', 'printBlockString']) {
      const originalMethod = exports[method];
      wrapped[method] = function (value, options) {
        const result = originalMethod.apply(this, arguments);
        capture.utilityCases.push({ test: testTitle(), sourceFile: 'blockString-test.ts', method, input: value, options: options ?? null, expected: result });
        return result;
      };
    }
    return wrapped;
  }

  if (request === '../printString' && parentFile.endsWith('/src/language/__tests__/printString-test.ts')) {
    const wrapped = { ...exports };
    const original = exports.printString;
    wrapped.printString = function (value) {
      const result = original.apply(this, arguments);
      capture.utilityCases.push({ test: testTitle(), sourceFile: 'printString-test.ts', method: 'printString', input: value, expected: result });
      return result;
    };
    return wrapped;
  }

  if (request === '../printLocation' && parentFile.endsWith('/src/language/__tests__/printLocation-test.ts')) {
    const wrapped = { ...exports };
    const original = exports.printSourceLocation;
    wrapped.printSourceLocation = function (source, location) {
      const result = original.apply(this, arguments);
      capture.locationPrintCases.push({
        test: testTitle(), sourceFile: 'printLocation-test.ts', body: source.body, name: source.name,
        locationOffset: source.locationOffset, location, expected: result,
      });
      return result;
    };
    return wrapped;
  }

  if (request === '../printer' && /\/src\/language\/__tests__\/(printer|schema-printer)-test\.ts$/.test(parentFile)) {
    const wrapped = { ...exports };
    const original = exports.print;
    wrapped.print = function (node) {
      try {
        const result = original.apply(this, arguments);
        const source = node?.loc?.source?.body ?? parsedSources.get(node)?.source;
        if (typeof source === 'string') capture.printCases.push({
          test: testTitle(), sourceFile: parentFile.split('/src/language/__tests__/')[1], source,
          options: parsedSources.get(node)?.options ?? null,
          nodeKind: node.kind,
          nodeLocation: node.loc ? { start: node.loc.start, end: node.loc.end } : null,
          expected: result,
        });
        else capture.excludedCases.push({ test: testTitle(), sourceFile: parentFile.split('/src/language/__tests__/')[1], reason: 'Printer case constructs a detached structural node rather than parsing a source document.' });
        return result;
      } catch (error) {
        capture.excludedCases.push({ test: testTitle(), sourceFile: parentFile.split('/src/language/__tests__/')[1], reason: 'Printer case asserts JavaScript-specific invalid-object diagnostics.' });
        throw error;
      }
    };
    return wrapped;
  }

  if (request === '../predicates' && parentFile.endsWith('/src/language/__tests__/predicates-test.ts')) {
    const wrapped = { ...exports };
    for (const method of Object.keys(exports)) {
      if (typeof exports[method] !== 'function') continue;
      const original = exports[method];
      wrapped[method] = function (node) {
        const result = original.apply(this, arguments);
        capture.predicateCases.push({ test: testTitle(), sourceFile: 'predicates-test.ts', method, node: canonicalNode(node), expected: result });
        return result;
      };
    }
    return wrapped;
  }

  if (request === '../source' && parentFile.endsWith('/src/language/__tests__/source-test.ts')) {
    const OriginalSource = exports.Source;
    class CapturedSource extends OriginalSource {
      constructor(body, name, locationOffset) {
        try {
          super(body, name, locationOffset);
          capture.sourceCases.push({ test: testTitle(), sourceFile: 'source-test.ts', body, name: this.name, locationOffset: this.locationOffset, outcome: 'valid' });
        } catch (error) {
          capture.sourceCases.push({ test: testTitle(), sourceFile: 'source-test.ts', body, name, locationOffset, outcome: 'invalid', errorName: error.name });
          throw error;
        }
      }
    }
    Object.defineProperty(CapturedSource, 'name', { value: 'Source' });
    return { ...exports, Source: CapturedSource };
  }

  if (request === '../schemaCoordinateLexer' && parentFile.endsWith('/src/language/__tests__/schemaCoordinateLexer-test.ts')) {
    const Original = exports.SchemaCoordinateLexer;
    const originalAdvance = Original.prototype.advance;
    Original.prototype.advance = function (...args) {
      try {
        const token = originalAdvance.apply(this, args);
        capture.coordinateLexerCases.push({ test: testTitle(), sourceFile: 'schemaCoordinateLexer-test.ts', source: this.source.body, token: { kind: token.kind, start: token.start, end: token.end, value: token.value } });
        return token;
      } catch (error) {
        capture.coordinateLexerCases.push({ test: testTitle(), sourceFile: 'schemaCoordinateLexer-test.ts', source: this.source.body, error: { message: error.message, position: error.positions?.[0] ?? null } });
        throw error;
      }
    };
    return exports;
  }

  if (request === '../visitor' && parentFile.endsWith('/src/language/__tests__/visitor-test.ts')) {
    const wrapped = { ...exports };
    const original = exports.visit;
    wrapped.visit = function (root, visitor, visitorKeyMap) {
      const source = root?.loc?.source?.body;
      try {
        const result = original.apply(this, arguments);
        if (typeof source === 'string') capture.visitorCases.push({ test: testTitle(), sourceFile: 'visitor-test.ts', source, expected: canonicalNode(result) });
        return result;
      } catch (error) {
        capture.excludedCases.push({ test: testTitle(), sourceFile: 'visitor-test.ts', reason: 'Visitor case asserts JavaScript-specific callback context or replacement behavior.' });
        throw error;
      }
    };
    return wrapped;
  }

  return exports;
};

process.on('exit', () => {
  const output = process.env.GRAPHQL_REFERENCE_CAPTURE_PATH;
  if (output) fs.writeFileSync(output, JSON.stringify(capture, null, 2) + '\n');
});

function canonicalNode(node) {
  if (Array.isArray(node)) return node.map(canonicalNode);
  if (!node || typeof node !== 'object') return node;
  if (!('kind' in node)) return node;
  const result = { kind: node.kind };
  for (const [key, value] of Object.entries(node)) {
    if (key === 'kind' || key === 'loc' || key === 'startToken' || key === 'endToken') continue;
    if (Array.isArray(value)) result[key] = value.map(canonicalNode);
    else if (value && typeof value === 'object' && 'kind' in value) result[key] = canonicalNode(value);
    else result[key] = value;
  }
  if (node.loc) result.loc = [node.loc.start, node.loc.end];
  return result;
}
