export type TokenExpiryDraft = {
  nonExpiring: boolean;
  days: number | null;
  confirmed: boolean;
};

export const tokenScopeDefinitions = [
  { id: 'mcp:read', description: ' (board and column reads)' },
  { id: 'mcp:write', description: ' (includes MCP board reads and mutations; excludes diagnostics)' },
  { id: 'mcp:diagnostics:read', description: ' (read system-wide diagnostics; requires an administrator)' },
  { id: 'api:read', description: ' (REST `GET` and `HEAD` on `/api/*`)' },
  { id: 'api:write', description: ' (REST `POST`/`PUT`/`PATCH`/`DELETE` on `/api/*`)' },
  { id: 'api:admin', description: ' (`/api/admin/*`)' },
  { id: 'api:system', description: ' (`/api/system/*`)' }
] as const;

export function allowedTokenScopes(scopes: readonly string[], allowed: readonly string[]): string[] {
  const allowedSet = new Set(allowed.map(scope => scope.toLowerCase()));
  return [...new Set(scopes.map(scope => scope.toLowerCase()))].filter(scope => allowedSet.has(scope));
}

export function createTokenExpiryDraft(): TokenExpiryDraft {
  return { nonExpiring: false, days: 30, confirmed: false };
}

export function tokenExpiryDays(expiry: TokenExpiryDraft): number | null {
  // On the wire null means non-expiring. An empty finite input must remain invalid.
  return expiry.nonExpiring ? null : (expiry.days ?? 0);
}
