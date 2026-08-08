import type { AppError } from '../types/appError';
import type { OAuthConnection } from '../types/oauthConnectionTypes';
import type { Result } from '../types/result';
import { ok } from '../types/result';
import { deleteJson, getEnvelope } from './http';

export type SystemOAuthConnectionsApi = ReturnType<typeof createSystemOAuthConnectionsApi>;

export function createSystemOAuthConnectionsApi() {
  async function getConnections(): Promise<Result<OAuthConnection[], AppError>> {
    const envelopeResult = await getEnvelope<OAuthConnection[]>('/api/system/oauth-connections');
    if (!envelopeResult.ok) {
      return envelopeResult;
    }

    return ok(envelopeResult.data.data ?? []);
  }

  async function revokeConnection(connectionId: number): Promise<Result<void, AppError>> {
    return deleteJson(`/api/system/oauth-connections/${connectionId}`);
  }

  return {
    getConnections,
    revokeConnection
  };
}

export const systemOAuthConnectionsApi = createSystemOAuthConnectionsApi();
