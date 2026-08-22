import { describe, expect, it } from 'vitest';
import type { OAuthTokenAudit } from '../../shared/types/oauthTokenAuditTypes';
import {
  buildOAuthTokenAuditMarkdown,
  formatConnection,
  formatGrantType,
  formatOwner
} from './oauthTokenAuditMarkdown';

describe('OAuth token audit Markdown', () => {
  it('builds a complete copyable audit report without raw credentials', () => {
    const markdown = buildOAuthTokenAuditMarkdown(newAudit(), '22 Aug 2026, 09:15');

    expect(markdown).toContain('# OAuth Log #42');
    expect(markdown).toContain('- **Occurred:** 22 Aug 2026, 09:15');
    expect(markdown).toContain('- **Grant type:** Refresh token');
    expect(markdown).toContain('- **Connection:** Luke’s Codex');
    expect(markdown).toContain('- **Owner:** luke (#7)');
    expect(markdown).toContain('- **OAuth client:** Codex (codex-client)');
    expect(markdown).toContain('## Token fingerprints');
    expect(markdown).toContain('- **Presented token:** sha256:presented');
    expect(markdown).toContain('- **Issued refresh token:** sha256:issued');
    expect(markdown).toContain('## Error');
    expect(markdown).not.toContain('raw-access-token');
  });

  it('uses safe fallbacks when connection and owner data is unavailable', () => {
    const audit = {
      ...newAudit(),
      grantType: 'client_credentials',
      oauthConnectionName: null,
      oauthClientDisplayName: null,
      oauthClientId: null,
      ownerUserId: null,
      ownerUserName: null
    };

    expect(formatGrantType(audit.grantType)).toBe('client_credentials');
    expect(formatConnection(audit)).toBe('-');
    expect(formatOwner(audit)).toBe('-');
  });
});

function newAudit(): OAuthTokenAudit {
  return {
    id: 42,
    occurredAtUtc: '2026-08-22T09:15:00Z',
    outcome: 'Rejected',
    grantType: 'refresh_token',
    requestedScopes: 'mcp:read',
    errorCode: 'invalid_grant',
    errorDescription: 'The refresh token has expired.',
    errorUri: 'https://example.test/errors/invalid_grant',
    presentedTokenFingerprint: 'sha256:presented',
    issuedRefreshTokenFingerprint: 'sha256:issued',
    authorizationId: 'authorization-42',
    oauthClientId: 'codex-client',
    oauthConnectionId: 9,
    oauthConnectionName: 'Luke’s Codex',
    ownerUserId: 7,
    ownerUserName: 'luke',
    oauthClientDisplayName: 'Codex',
    resource: 'https://boardoil.example.test/mcp',
    traceIdentifier: 'trace-42',
    userAgent: 'Codex/1.0'
  };
}
