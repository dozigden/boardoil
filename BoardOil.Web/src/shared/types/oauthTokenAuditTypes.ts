export type OAuthTokenAudit = {
  id: number;
  occurredAtUtc: string;
  outcome: string;
  grantType: string;
  requestedScopes: string | null;
  errorCode: string | null;
  errorDescription: string | null;
  errorUri: string | null;
  presentedTokenFingerprint: string | null;
  issuedRefreshTokenFingerprint: string | null;
  authorizationId: string | null;
  oauthClientId: string | null;
  oauthConnectionId: number | null;
  oauthConnectionName: string | null;
  ownerUserId: number | null;
  ownerUserName: string | null;
  oauthClientDisplayName: string | null;
  resource: string | null;
  traceIdentifier: string | null;
  userAgent: string | null;
};

export type OAuthTokenAuditList = {
  items: OAuthTokenAudit[];
  offset: number;
  limit: number;
  totalCount: number;
};

export type OAuthTokenAuditPurgeResult = {
  retentionDays: number;
  cutoffUtc: string;
  deletedCount: number;
};
