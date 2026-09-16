import { describe, expect, it, vi } from 'vitest';
import type { Router } from 'vue-router';
import { installRouteChunkRecovery, isRouteChunkLoadError } from './routeChunkRecovery';

describe('routeChunkRecovery', () => {
  it.each([
    'Failed to fetch dynamically imported module: https://boardoil.example/assets/LoginView-old.js',
    'error loading dynamically imported module: https://boardoil.example/assets/UnauthorizedView-old.js',
    'Importing a module script failed.',
    'ChunkLoadError: Loading chunk 42 failed.'
  ])('recognises route chunk failures: %s', message => {
    expect(isRouteChunkLoadError(new TypeError(message))).toBe(true);
  });

  it('reloads once when a route chunk cannot be loaded', () => {
    const router = createRouterHarness();
    const storage = createStorage();
    const reload = vi.fn();
    installRouteChunkRecovery(router.router, {
      getCurrentUrl: () => 'https://boardoil.example/boards/1',
      getStorage: () => storage,
      reload
    });

    const error = new TypeError(
      'error loading dynamically imported module: https://boardoil.example/assets/UnauthorizedView-old.js'
    );
    router.raiseError(error);
    router.raiseError(error);

    expect(reload).toHaveBeenCalledOnce();
  });

  it('clears the reload guard after a successful navigation', () => {
    const router = createRouterHarness();
    const storage = createStorage();
    const reload = vi.fn();
    installRouteChunkRecovery(router.router, {
      getCurrentUrl: () => 'https://boardoil.example/boards/1',
      getStorage: () => storage,
      reload
    });

    const error = new TypeError('Failed to fetch dynamically imported module: /assets/BoardsView-old.js');
    router.raiseError(error);
    router.completeNavigation();
    router.raiseError(error);

    expect(reload).toHaveBeenCalledTimes(2);
  });

  it('leaves unrelated router errors alone', () => {
    const router = createRouterHarness();
    const reload = vi.fn();
    installRouteChunkRecovery(router.router, {
      getCurrentUrl: () => 'https://boardoil.example/boards/1',
      getStorage: () => createStorage(),
      reload
    });

    router.raiseError(new Error('A navigation guard failed.'));

    expect(reload).not.toHaveBeenCalled();
  });

  it('does not reload when session storage is unavailable', () => {
    const router = createRouterHarness();
    const reload = vi.fn();
    installRouteChunkRecovery(router.router, {
      getCurrentUrl: () => 'https://boardoil.example/boards/1',
      getStorage: () => {
        throw new Error('Storage is blocked.');
      },
      reload
    });

    router.raiseError(new TypeError('Importing a module script failed.'));

    expect(reload).not.toHaveBeenCalled();
  });
});

function createRouterHarness() {
  let errorHandler: ((error: unknown) => void) | null = null;
  let afterEachHandler: ((_to: unknown, _from: unknown, failure?: unknown) => void) | null = null;
  const router = {
    onError: vi.fn(handler => {
      errorHandler = handler;
      return () => undefined;
    }),
    afterEach: vi.fn(handler => {
      afterEachHandler = handler;
      return () => undefined;
    })
  } as unknown as Pick<Router, 'afterEach' | 'onError'>;

  return {
    router,
    raiseError(error: unknown) {
      expect(errorHandler).not.toBeNull();
      errorHandler?.(error);
    },
    completeNavigation() {
      expect(afterEachHandler).not.toBeNull();
      afterEachHandler?.({}, {}, undefined);
    }
  };
}

function createStorage() {
  const values = new Map<string, string>();
  return {
    getItem: (key: string) => values.get(key) ?? null,
    setItem: (key: string, value: string) => values.set(key, value),
    removeItem: (key: string) => values.delete(key)
  };
}
