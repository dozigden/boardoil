import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createClientErrorsApi } from './clientErrorsApi';
import { postJsonQuiet } from './http';

vi.mock('./http', () => ({
  postJsonQuiet: vi.fn()
}));

describe('clientErrorsApi', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('reports client diagnostics through the quiet transport', async () => {
    const request = {
      message: 'render failed',
      exceptionType: 'TypeError',
      stackTrace: 'at render',
      phase: 'vue',
      routeName: 'board',
      routePath: '/boards/1',
      frontendVersion: '1.4.0 (dev/local) abc123',
      viewport: { width: 1280, height: 720 },
      userAgent: 'Test Browser',
      context: { componentName: 'BoardView' }
    };

    await createClientErrorsApi().reportClientError(request);

    expect(postJsonQuiet).toHaveBeenCalledWith(
      '/api/system/error-logs:report-client-error',
      request
    );
  });
});
