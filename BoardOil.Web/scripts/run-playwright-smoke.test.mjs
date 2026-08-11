import { describe, expect, it, vi } from 'vitest';
import {
  parseRunnerArguments,
  runPlaywrightTests
} from './run-playwright-smoke.mjs';

describe('external Playwright runtime mode', () => {
  it('requires an explicit external base URL', () => {
    expect(() => parseRunnerArguments(['--external-runtime'], {})).toThrow(
      'BOARDOIL_E2E_BASE_URL is required when using --external-runtime.'
    );
  });

  it.each([
    'boardoil.test',
    'file:///tmp/boardoil'
  ])('rejects unsupported base URL %s', baseUrl => {
    expect(() => parseRunnerArguments(['--external-runtime'], {
      BOARDOIL_E2E_BASE_URL: baseUrl
    })).toThrow();
  });

  it('preserves profile selection and Playwright CLI arguments', () => {
    const options = parseRunnerArguments([
      '--external-runtime',
      '\\.smoke\\.spec\\.ts$',
      '--grep',
      'creates a board',
      '--reporter=list'
    ], {
      BOARDOIL_E2E_BASE_URL: ' https://boardoil.test:8443 '
    });

    expect(options).toEqual({
      mode: 'external',
      baseUrl: 'https://boardoil.test:8443',
      playwrightArguments: [
        '\\.smoke\\.spec\\.ts$',
        '--grep',
        'creates a board',
        '--reporter=list'
      ]
    });
  });

  it('selects only the external runner', async () => {
    const runManaged = vi.fn();
    const runExternal = vi.fn().mockResolvedValue(7);
    const options = {
      mode: 'external',
      baseUrl: 'http://127.0.0.1:8080',
      playwrightArguments: ['--project=chromium']
    };

    const exitCode = await runPlaywrightTests(options, {
      runManaged,
      runExternal
    });

    expect(exitCode).toBe(7);
    expect(runManaged).not.toHaveBeenCalled();
    expect(runExternal).toHaveBeenCalledExactlyOnceWith(options);
  });
});
