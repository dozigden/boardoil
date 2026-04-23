import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

type ConfigModule = {
  apiBase: string;
  boardHubUrl: string;
  buildApiUrl: (path: string) => string;
};

async function loadConfigModule() {
  return (await import('./config')) as ConfigModule;
}

describe('api config', () => {
  beforeEach(() => {
    vi.resetModules();
    vi.unstubAllEnvs();
    vi.stubGlobal('window', {
      location: {
        origin: 'http://localhost:5173'
      }
    });
  });

  afterEach(() => {
    vi.unstubAllEnvs();
    vi.unstubAllGlobals();
  });

  it('uses same-origin base by default when VITE_API_BASE is unset', async () => {
    vi.stubEnv('VITE_API_BASE', '');

    const config = await loadConfigModule();

    expect(config.apiBase).toBe('http://localhost:5173');
    expect(config.boardHubUrl).toBe('http://localhost:5173/hubs/board');
    expect(config.buildApiUrl('/api/board')).toBe('http://localhost:5173/api/board');
  });

  it('normalizes VITE_API_BASE by trimming trailing slashes', async () => {
    vi.stubEnv('VITE_API_BASE', 'https://api.example.test///');

    const config = await loadConfigModule();

    expect(config.apiBase).toBe('https://api.example.test');
    expect(config.boardHubUrl).toBe('https://api.example.test/hubs/board');
    expect(config.buildApiUrl('/api/board')).toBe('https://api.example.test/api/board');
  });
});
