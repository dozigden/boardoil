import { describe, expect, it } from 'vitest';
import type { ErrorLogDetails } from '../../shared/types/errorLogTypes';
import {
  buildErrorLogMarkdown,
  formatErrorLogContextJson,
  formatErrorLogStackTrace
} from './errorLogMarkdown';

describe('error log Markdown', () => {
  it('builds the complete copyable diagnostic report', () => {
    const markdown = buildErrorLogMarkdown(newErrorLog(), '9 Aug 2026, 12:00');

    expect(markdown).toContain('# Error #42');
    expect(markdown).toContain('- **Occurred:** 9 Aug 2026, 12:00');
    expect(markdown).toContain('- **Actor:** #7');
    expect(markdown).toContain('- **Request:** POST /api/cards');
    expect(markdown).toContain('## Exception\n\nSystem.InvalidOperationException\n\nCard failed.');
    expect(markdown).toContain('```text\nfirst line\nsecond line\n```');
    expect(markdown).toContain('```json\n{\n  "endpoint": "card-create"\n}\n```');
  });

  it('normalises escaped stack newlines and preserves invalid context text', () => {
    expect(formatErrorLogStackTrace('first\\r\\nsecond\\nthird')).toBe('first\nsecond\nthird');
    expect(formatErrorLogContextJson('not-json')).toBe('not-json');
  });

  it('represents frontend entries with their route and structured context', () => {
    const frontendError = {
      ...newErrorLog(),
      source: 'Frontend',
      area: 'WebClient',
      requestMethod: null,
      requestPath: '/boards/7?search=music',
      contextJson: '{"phase":"vue","routePath":"/boards/7?search=music"}'
    };

    const markdown = buildErrorLogMarkdown(frontendError, '9 Aug 2026, 12:00');

    expect(markdown).toContain('- **Source:** Frontend');
    expect(markdown).toContain('- **Area:** WebClient');
    expect(markdown).toContain('- **Request:** /boards/7?search=music');
    expect(markdown).toContain('"phase": "vue"');
  });
});

function newErrorLog(): ErrorLogDetails {
  return {
    id: 42,
    occurredAtUtc: '2026-08-09T12:00:00Z',
    source: 'Backend',
    area: 'ApiRequest',
    exceptionType: 'System.InvalidOperationException',
    message: 'Card failed.',
    stackTrace: 'first line\\nsecond line',
    traceIdentifier: 'trace-42',
    requestMethod: 'POST',
    requestPath: '/api/cards',
    actorUserId: 7,
    contextJson: '{"endpoint":"card-create"}',
    createdAtUtc: '2026-08-09T12:00:00Z',
    updatedAtUtc: '2026-08-09T12:00:00Z'
  };
}
