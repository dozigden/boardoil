import type { AppError } from '../types/appError';
import type {
  OAuthTokenAuditList,
  OAuthTokenAuditPurgeResult
} from '../types/oauthTokenAuditTypes';
import type { Result } from '../types/result';
import { err, ok } from '../types/result';
import { getEnvelope, postData } from './http';

export type OAuthTokenAuditsApi = ReturnType<typeof createOAuthTokenAuditsApi>;

export function createOAuthTokenAuditsApi() {
  async function getOAuthTokenAudits(
    offset: number,
    limit: number
  ): Promise<Result<OAuthTokenAuditList, AppError>> {
    const result = await getEnvelope<OAuthTokenAuditList>(
      `/api/system/oauth-token-audits?offset=${offset}&limit=${limit}`
    );
    if (!result.ok) {
      return result;
    }

    if (!result.data.data) {
      return err({
        kind: 'api',
        message: result.data.message ?? 'Failed to load OAuth logs.'
      });
    }

    return ok(result.data.data);
  }

  async function purgeExpiredOAuthTokenAudits(): Promise<Result<OAuthTokenAuditPurgeResult, AppError>> {
    return postData<OAuthTokenAuditPurgeResult>('/api/system/oauth-token-audits:purge', {});
  }

  return { getOAuthTokenAudits, purgeExpiredOAuthTokenAudits };
}

export const oauthTokenAuditsApi = createOAuthTokenAuditsApi();
