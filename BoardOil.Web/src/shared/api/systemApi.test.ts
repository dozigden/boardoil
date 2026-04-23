import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ok } from '../types/result';

const putJson = vi.fn();
const postData = vi.fn();
const putData = vi.fn();

vi.mock('./http', () => ({
  deleteJson: vi.fn(),
  getEnvelope: vi.fn(),
  patchData: vi.fn(),
  postData: (...args: unknown[]) => postData(...args),
  putData: (...args: unknown[]) => putData(...args),
  putJson: (...args: unknown[]) => putJson(...args)
}));

import { createSystemApi } from './systemApi';

describe('systemApi', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    putJson.mockResolvedValue(ok(undefined));
    postData.mockResolvedValue(ok(undefined));
    putData.mockResolvedValue(ok(undefined));
  });

  it('createUser posts email in the system user payload', async () => {
    const api = createSystemApi();

    await api.createUser('member', 'member@example.test', 'Password1234!', 'Standard');

    expect(postData).toHaveBeenCalledWith('/api/system/users', {
      userName: 'member',
      email: 'member@example.test',
      password: 'Password1234!',
      role: 'Standard'
    });
  });

  it('updateUser puts email role and status to the combined endpoint', async () => {
    const api = createSystemApi();

    await api.updateUser(42, {
      email: 'member@example.test',
      role: 'Admin',
      isActive: false
    });

    expect(putData).toHaveBeenCalledWith('/api/system/users/42', {
      email: 'member@example.test',
      role: 'Admin',
      isActive: false
    });
  });

  it('updateClientAccount puts email role and status to the client endpoint', async () => {
    const api = createSystemApi();

    await api.updateClientAccount(7, {
      email: 'client@example.test',
      role: 'Standard',
      isActive: true
    });

    expect(putData).toHaveBeenCalledWith('/api/system/client-accounts/7', {
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
});
