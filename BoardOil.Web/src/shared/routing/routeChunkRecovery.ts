import type { Router } from 'vue-router';

const RouteChunkReloadMarkerKey = 'boardoil:route-chunk-reload-url';

type RecoveryStorage = Pick<Storage, 'getItem' | 'setItem' | 'removeItem'>;

type RouteChunkRecoveryOptions = {
  getCurrentUrl?: () => string;
  getStorage?: () => RecoveryStorage;
  reload?: () => void;
};

type RecoveryRouter = Pick<Router, 'afterEach' | 'onError'>;

export function installRouteChunkRecovery(
  router: RecoveryRouter,
  options: RouteChunkRecoveryOptions = {}
) {
  const getCurrentUrl = options.getCurrentUrl ?? (() => window.location.href);
  const getStorage = options.getStorage ?? (() => window.sessionStorage);
  const reload = options.reload ?? (() => window.location.reload());

  router.onError(error => {
    if (!isRouteChunkLoadError(error)) {
      return;
    }

    const currentUrl = getCurrentUrl();
    if (!reserveReload(getStorage, currentUrl)) {
      return;
    }

    reload();
  });

  router.afterEach((_to, _from, failure) => {
    if (failure) {
      return;
    }

    clearReloadMarker(getStorage);
  });
}

export function isRouteChunkLoadError(error: unknown) {
  const errorText = getErrorText(error);
  return errorText.includes('dynamically imported module')
    || errorText.includes('importing a module script failed')
    || errorText.includes('failed to load module script')
    || errorText.includes('chunkloaderror')
    || /loading (?:css )?chunk .+ failed/.test(errorText)
    || errorText.includes('unable to preload css');
}

function reserveReload(getStorage: () => RecoveryStorage, currentUrl: string) {
  try {
    const storage = getStorage();
    if (storage.getItem(RouteChunkReloadMarkerKey) === currentUrl) {
      return false;
    }

    storage.setItem(RouteChunkReloadMarkerKey, currentUrl);
    return true;
  } catch {
    return false;
  }
}

function clearReloadMarker(getStorage: () => RecoveryStorage) {
  try {
    getStorage().removeItem(RouteChunkReloadMarkerKey);
  } catch {
    // Without session storage, recovery remains disabled to avoid a reload loop.
  }
}

function getErrorText(error: unknown) {
  if (error instanceof Error) {
    return `${error.name} ${error.message}`.toLowerCase();
  }

  if (typeof error === 'string') {
    return error.toLowerCase();
  }

  if (typeof error === 'object' && error !== null && 'message' in error) {
    const message = error.message;
    if (typeof message === 'string') {
      return message.toLowerCase();
    }
  }

  return '';
}
