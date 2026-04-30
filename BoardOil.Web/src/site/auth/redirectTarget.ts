const REDIRECT_QUERY_KEY = 'redirect';

export function buildLoginRedirectQuery(targetPath: string | undefined) {
  if (!targetPath || !isSafeInternalPath(targetPath)) {
    return undefined;
  }

  return { [REDIRECT_QUERY_KEY]: targetPath };
}

export function getSafeRedirectTarget(value: unknown) {
  if (typeof value !== 'string' || !isSafeInternalPath(value)) {
    return null;
  }

  if (value === '/login' || value.startsWith('/login?')) {
    return null;
  }

  if (value === '/setup-initial-admin' || value.startsWith('/setup-initial-admin?')) {
    return null;
  }

  if (value === '/unauthorized' || value.startsWith('/unauthorized?')) {
    return null;
  }

  return value;
}

function isSafeInternalPath(path: string) {
  return path.startsWith('/') && !path.startsWith('//');
}
