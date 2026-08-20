import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ok } from '../types/result';

const deleteJson = vi.fn();
const getEnvelope = vi.fn();
const getJson = vi.fn();

vi.mock('./http', () => ({
  deleteJson: (...args: unknown[]) => deleteJson(...args),
  getEnvelope: (...args: unknown[]) => getEnvelope(...args),
  getJson: (...args: unknown[]) => getJson(...args)
}));

import { getMcpOAuthMetadata } from './oauthMetadataApi';
import { createOAuthConnectionsApi } from './oauthConnectionsApi';
import { createSystemOAuthConnectionsApi } from './systemOAuthConnectionsApi';

describe('oauthConnectionsApi', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    deleteJson.mockResolvedValue(ok(undefined));
    getJson.mockResolvedValue(ok({ resource: 'https://boardoil.example.com/mcp' }));
    getEnvelope.mockResolvedValue(ok({
      success: true,
      statusCode: 200,
      message: null,
      data: []
    }));
  });

  it('uses owner-scoped endpoints for personal management', async () => {
    const api = createOAuthConnectionsApi();

    await api.getOwnConnections();
    await api.revokeOwnConnection(17);

    expect(getEnvelope).toHaveBeenCalledWith('/api/oauth-connections');
    expect(deleteJson).toHaveBeenCalledWith('/api/oauth-connections/17');
  });

  it('uses administrator endpoints for system management', async () => {
    const api = createSystemOAuthConnectionsApi();

    await api.getConnections();
    await api.revokeConnection(23);

    expect(getEnvelope).toHaveBeenCalledWith('/api/system/oauth-connections');
    expect(deleteJson).toHaveBeenCalledWith('/api/system/oauth-connections/23');
  });

  it('gets the canonical MCP OAuth resource from public metadata', async () => {
    await getMcpOAuthMetadata();

    expect(getJson).toHaveBeenCalledWith('/.well-known/oauth-protected-resource/mcp');
  });
});
