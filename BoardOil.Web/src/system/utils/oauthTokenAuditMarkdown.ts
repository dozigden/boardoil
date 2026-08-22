import type { OAuthTokenAudit } from '../../shared/types/oauthTokenAuditTypes';

export function buildOAuthTokenAuditMarkdown(
  audit: OAuthTokenAudit,
  occurredLabel: string
): string {
  return [
    `# OAuth Log #${audit.id}`,
    '',
    `- **Occurred:** ${occurredLabel}`,
    `- **Outcome:** ${audit.outcome}`,
    `- **Grant type:** ${formatGrantType(audit.grantType)}`,
    `- **Requested scopes:** ${formatValue(audit.requestedScopes)}`,
    `- **Connection:** ${formatConnection(audit)}`,
    `- **Connection ID:** ${formatReference(audit.oauthConnectionId)}`,
    `- **Owner:** ${formatOwner(audit)}`,
    `- **OAuth client:** ${formatOAuthClient(audit)}`,
    `- **Authorization ID:** ${formatValue(audit.authorizationId)}`,
    `- **Resource:** ${formatValue(audit.resource)}`,
    `- **Trace:** ${formatValue(audit.traceIdentifier)}`,
    `- **User agent:** ${formatValue(audit.userAgent)}`,
    '',
    '## Token fingerprints',
    '',
    `- **Presented token:** ${formatValue(audit.presentedTokenFingerprint)}`,
    `- **Issued refresh token:** ${formatValue(audit.issuedRefreshTokenFingerprint)}`,
    '',
    '## Error',
    '',
    `- **Code:** ${formatValue(audit.errorCode)}`,
    `- **Description:** ${formatValue(audit.errorDescription)}`,
    `- **URI:** ${formatValue(audit.errorUri)}`
  ].join('\n');
}

export function formatGrantType(grantType: string): string {
  if (grantType === 'authorization_code') {
    return 'Authorization code';
  }

  if (grantType === 'refresh_token') {
    return 'Refresh token';
  }

  return grantType || 'Unknown';
}

export function formatConnection(audit: OAuthTokenAudit): string {
  return audit.oauthConnectionName
    ?? audit.oauthClientDisplayName
    ?? audit.oauthClientId
    ?? '-';
}

export function formatOwner(audit: OAuthTokenAudit): string {
  if (audit.ownerUserName && audit.ownerUserId !== null) {
    return `${audit.ownerUserName} (#${audit.ownerUserId})`;
  }

  if (audit.ownerUserName) {
    return audit.ownerUserName;
  }

  return formatReference(audit.ownerUserId);
}

export function formatOAuthClient(audit: OAuthTokenAudit): string {
  if (audit.oauthClientDisplayName && audit.oauthClientId) {
    return `${audit.oauthClientDisplayName} (${audit.oauthClientId})`;
  }

  return audit.oauthClientDisplayName ?? audit.oauthClientId ?? '-';
}

export function formatReference(value: number | null): string {
  return value === null ? '-' : `#${value}`;
}

export function formatValue(value: string | null): string {
  return value?.trim() || '-';
}
