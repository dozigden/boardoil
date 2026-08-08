import type { AppError } from '../types/appError';
import type { OAuthConnection } from '../types/oauthConnectionTypes';
import type { Result } from '../types/result';
import { ok } from '../types/result';
import { deleteJson, getEnvelope } from './http';

export type OAuthConnectionsApi = ReturnType<typeof createOAuthConnectionsApi>;

export function createOAuthConnectionsApi() {
  async function getOwnConnections(): Promise<Result<OAuthConnection[], AppError>> {
    return getConnections('/api/oauth-connections');
  }

  async function revokeOwnConnection(connectionId: number): Promise<Result<void, AppError>> {
    return deleteJson(`/api/oauth-connections/${connectionId}`);
  }

  async function getConnections(path: string): Promise<Result<OAuthConnection[], AppError>> {
    const envelopeResult = await getEnvelope<OAuthConnection[]>(path);
    if (!envelopeResult.ok) {
      return envelopeResult;
    }

    return ok(envelopeResult.data.data ?? []);
  }

  return {
    getOwnConnections,
    revokeOwnConnection
  };
}

export const oauthConnectionsApi = createOAuthConnectionsApi();
