import { afterEach, describe, expect, it, vi } from 'vitest';
import type { App } from 'vue';
import type { Router } from 'vue-router';
import type { ClientErrorReportRequest, ClientErrorsApi } from '../api/clientErrorsApi';
import {
  createClientErrorReporter,
  installFrontendErrorReporting
} from './clientErrorReporter';

vi.mock('../api/clientErrorsApi', () => ({
  createClientErrorsApi: () => ({
    reportClientError: vi.fn()
  })
}));

vi.mock('../api/versionApi', () => ({
  getFrontendBuildInfo: () => ({
    version: '1.4.0',
    channel: 'dev',
    build: 'local',
    commit: 'abc123'
  })
}));

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('clientErrorReporter', () => {
  it('normalises errors and preserves route query and fragment metadata', async () => {
    const sent: ClientErrorReportRequest[] = [];
    const reporter = createReporter(sent, {
      routeName: 'board',
      routePath: '/boards/7?search=music#dialog'
    });

    const reported = await reporter.reportError(
      new TypeError('Client exploded.'),
      'vue',
      {
        vueInfo: 'render',
        accessToken: 'never-store',
        nested: { status: 'broken', cookie: 'never-store' },
        content: 'never-store'
      });

    expect(reported).toBe(true);
    expect(sent).toHaveLength(1);
    expect(sent[0]).toMatchObject({
      message: 'Client exploded.',
      exceptionType: 'TypeError',
      phase: 'vue',
      routeName: 'board',
      routePath: '/boards/7?search=music#dialog',
      frontendVersion: '1.4.0 (dev/local) abc123',
      viewport: { width: 1280, height: 720 },
      userAgent: 'Test Browser'
    });
    expect(sent[0].context).toEqual({
      vueInfo: 'render',
      nested: { status: 'broken' }
    });
  });

  it('deduplicates general error fingerprints for one minute', async () => {
    const sent: ClientErrorReportRequest[] = [];
    let now = 1_000;
    const reporter = createReporter(sent, undefined, () => now);

    await reporter.reportError(new Error('same'), 'vue');
    await reporter.reportError(new Error('same'), 'vue');
    now += 60_001;
    await reporter.reportError(new Error('same'), 'vue');

    expect(sent).toHaveLength(2);
  });

  it('caps general reports per minute', async () => {
    const sent: ClientErrorReportRequest[] = [];
    const reporter = createReporter(sent);

    for (let index = 0; index < 11; index++) {
      await reporter.reportError(new Error(`error-${index}`), 'vue');
    }

    expect(sent).toHaveLength(10);
  });

  it('uses stricter throttling for realtime diagnostics', async () => {
    const sent: ClientErrorReportRequest[] = [];
    const reporter = createReporter(sent);

    for (let index = 0; index < 4; index++) {
      await reporter.reportRealtimeDiagnostic(
        'realtime-start-failed',
        new Error(`error-${index}`),
        { reconnectAttempt: index + 1 });
    }

    expect(sent).toHaveLength(3);
    expect(sent.every(report => report.phase === 'realtime-start-failed')).toBe(true);
  });

  it('swallows API and context sanitisation failures', async () => {
    const reportClientError = vi.fn()
      .mockResolvedValueOnce(undefined)
      .mockRejectedValueOnce(new Error('network down'));
    const api: ClientErrorsApi = { reportClientError };
    const reporter = createClientErrorReporter({
      api,
      buildInfoProvider: () => null,
      routeProvider: () => ({ routeName: null, routePath: null }),
      userAgentProvider: () => null,
      viewportProvider: () => null
    });
    const context = {} as Record<string, unknown>;
    Object.defineProperty(context, 'broken', {
      enumerable: true,
      get: () => {
        throw new Error('context getter failed');
      }
    });

    const contextResult = await reporter.reportError(new Error('context failure'), 'vue', context);
    const apiResult = await reporter.reportError(new Error('api failure'), 'vue');

    expect(contextResult).toBe(true);
    expect(apiResult).toBe(false);
    expect(reportClientError.mock.calls[0]?.[0]?.context).toBeNull();
  });

  it('normalises scalar properties on error-like rejection objects', async () => {
    const sent: ClientErrorReportRequest[] = [];
    const reporter = createReporter(sent);

    const reported = await reporter.reportError({
      name: 404,
      message: 503,
      stack: { unavailable: true }
    }, 'unhandled-rejection');

    expect(reported).toBe(true);
    expect(sent[0]).toMatchObject({
      exceptionType: '404',
      message: '503',
      stackTrace: null
    });
  });

  it('installs Vue, window error, and unhandled rejection handlers using full paths', () => {
    const reportError = vi.fn().mockResolvedValue(true);
    const reporter = {
      reportError,
      reportRealtimeDiagnostic: vi.fn(),
      setRouteProvider: vi.fn()
    };
    const app = { config: {} } as App;
    const router = {
      currentRoute: {
        value: {
          name: 'board',
          path: '/boards/7',
          fullPath: '/boards/7?search=books#results'
        }
      }
    } as Router;
    const handlers: Record<string, (event: never) => void> = {};
    vi.stubGlobal('window', {
      addEventListener: vi.fn((name: string, handler: (event: never) => void) => {
        handlers[name] = handler;
      })
    });

    installFrontendErrorReporting(app, router, reporter);
    app.config.errorHandler!(new Error('vue failed'), null, 'render');
    handlers.error?.({
      error: new Error('window failed'),
      message: 'window failed',
      filename: '/assets/app.js?build=local',
      lineno: 12,
      colno: 4
    } as never);
    handlers.unhandledrejection?.({ reason: new Error('promise failed') } as never);

    expect(reporter.setRouteProvider).toHaveBeenCalledOnce();
    const routeProvider = reporter.setRouteProvider.mock.calls[0]?.[0];
    expect(routeProvider()).toEqual({
      routeName: 'board',
      routePath: '/boards/7?search=books#results'
    });
    expect(reportError).toHaveBeenCalledWith(expect.any(Error), 'vue', {
      vueInfo: 'render',
      componentName: null
    });
    expect(reportError).toHaveBeenCalledWith(expect.any(Error), 'window-error', {
      fileName: '/assets/app.js?build=local',
      lineNumber: 12,
      columnNumber: 4
    });
    expect(reportError).toHaveBeenCalledWith(expect.any(Error), 'unhandled-rejection');
  });
});

function createReporter(
  sent: ClientErrorReportRequest[],
  route = { routeName: null, routePath: null } as { routeName: string | null; routePath: string | null },
  now = () => 1_000
) {
  const api: ClientErrorsApi = {
    reportClientError: async request => {
      sent.push(request);
    }
  };
  return createClientErrorReporter({
    api,
    now,
    routeProvider: () => route,
    buildInfoProvider: () => '1.4.0 (dev/local) abc123',
    viewportProvider: () => ({ width: 1280, height: 720 }),
    userAgentProvider: () => 'Test Browser'
  });
}
