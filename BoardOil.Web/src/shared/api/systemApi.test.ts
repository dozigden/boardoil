import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ok } from '../types/result';

const putJson = vi.fn();
const postData = vi.fn();
const postFormData = vi.fn();
const putData = vi.fn();
const deleteJson = vi.fn();
const getEnvelope = vi.fn();

vi.mock('./http', () => ({
  deleteJson: (...args: unknown[]) => deleteJson(...args),
  getEnvelope: (...args: unknown[]) => getEnvelope(...args),
  patchData: vi.fn(),
  postData: (...args: unknown[]) => postData(...args),
  postFormData: (...args: unknown[]) => postFormData(...args),
  putData: (...args: unknown[]) => putData(...args),
  putJson: (...args: unknown[]) => putJson(...args)
}));

import { createSystemApi } from './systemApi';

describe('systemApi', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    putJson.mockResolvedValue(ok(undefined));
    postData.mockResolvedValue(ok(undefined));
    postFormData.mockResolvedValue(ok(undefined));
    putData.mockResolvedValue(ok(undefined));
    deleteJson.mockResolvedValue(ok(undefined));
    getEnvelope.mockReset();
  });

  it('createUser posts email in the system user payload', async () => {
    const api = createSystemApi();

    await api.createUser({
      userName: 'member',
      displayName: 'Member',
      email: 'member@example.test',
      password: 'Password1234!',
      role: 'Standard'
    });

    expect(postData).toHaveBeenCalledWith('/api/system/users', {
      userName: 'member',
      displayName: 'Member',
      email: 'member@example.test',
      password: 'Password1234!',
      role: 'Standard'
    });
  });

  it('updateUser puts email role and status to the combined endpoint', async () => {
    const api = createSystemApi();

    await api.updateUser(42, {
      displayName: 'Member Updated',
      email: 'member@example.test',
      role: 'Admin',
      isActive: false
    });

    expect(putData).toHaveBeenCalledWith('/api/system/users/42', {
      displayName: 'Member Updated',
      email: 'member@example.test',
      role: 'Admin',
      isActive: false
    });
  });

  it('updateClientAccount puts email role and status to the client endpoint', async () => {
    const api = createSystemApi();

    await api.updateClientAccount(7, {
      displayName: 'Client Updated',
      email: 'client@example.test',
      role: 'Standard',
      isActive: true
    });

    expect(putData).toHaveBeenCalledWith('/api/system/client-accounts/7', {
      displayName: 'Client Updated',
      email: 'client@example.test',
      role: 'Standard',
      isActive: true
    });
  });

  it('resetUserPassword puts to the system user password endpoint', async () => {
    const api = createSystemApi();

    await api.resetUserPassword(42, 'FreshPassword1234!');

    expect(putJson).toHaveBeenCalledWith('/api/system/users/42/password', {
      newPassword: 'FreshPassword1234!'
    });
  });

  it('uploadClientAccountProfileImage posts multipart form-data to the profile-image endpoint', async () => {
    const api = createSystemApi();
    const file = new File(['image'], 'avatar.png', { type: 'image/png' });

    await api.uploadClientAccountProfileImage(7, file);

    expect(postFormData).toHaveBeenCalledTimes(1);
    expect(postFormData).toHaveBeenCalledWith(
      '/api/system/client-accounts/7/profile-image',
      expect.any(FormData)
    );
  });

  it('deleteClientAccountProfileImage deletes from the profile-image endpoint', async () => {
    const api = createSystemApi();

    await api.deleteClientAccountProfileImage(7);

    expect(deleteJson).toHaveBeenCalledWith('/api/system/client-accounts/7/profile-image');
  });

  it('getMcpProjectConnections reads the project connection collection', async () => {
    getEnvelope.mockResolvedValueOnce(ok({
      success: true,
      statusCode: 200,
      message: null,
      data: []
    }));
    const api = createSystemApi();

    const result = await api.getMcpProjectConnections();

    expect(getEnvelope).toHaveBeenCalledWith('/api/system/mcp-project-connections');
    expect(result).toEqual(ok([]));
  });

  it('createMcpProjectConnection posts the owning client and scopes', async () => {
    const api = createSystemApi();
    const request = {
      clientAccountId: 7,
      name: 'Repository',
      allowedScopes: ['mcp:read' as const]
    };

    await api.createMcpProjectConnection(request);

    expect(postData).toHaveBeenCalledWith('/api/system/mcp-project-connections', request);
  });

  it('revokeMcpProjectConnection deletes the active project connection resource', async () => {
    const api = createSystemApi();

    await api.revokeMcpProjectConnection(14);

    expect(deleteJson).toHaveBeenCalledWith('/api/system/mcp-project-connections/14');
  });

  it('getSystemInfoMessage reads from the system-info endpoint', async () => {
    getEnvelope.mockResolvedValueOnce(ok({
      success: true,
      statusCode: 200,
      message: null,
      data: null
    }));
    const api = createSystemApi();

    const result = await api.getSystemInfoMessage();

    expect(getEnvelope).toHaveBeenCalledWith('/api/system/system-info-message');
    expect(result.ok).toBe(true);
    if (result.ok) {
      expect(result.data).toBeNull();
    }
  });
});
