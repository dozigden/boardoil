import type { OAuthProtectedResourceMetadata } from '../types/oauthConnectionTypes';
import { getJson } from './http';

export function getMcpOAuthMetadata() {
  return getJson<OAuthProtectedResourceMetadata>(
    '/.well-known/oauth-protected-resource/mcp'
  );
}
