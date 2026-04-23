import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ok } from '../types/result';

const postData = vi.fn();
const postJson = vi.fn();
const getEnvelope = vi.fn();
const deleteJson = vi.fn();

vi.mock('./http', () => ({
  postData: (...args: unknown[]) => postData(...args),
  postJson: (...args: unknown[]) => postJson(...args),
  getEnvelope: (...args: unknown[]) => getEnvelope(...args),
  deleteJson: (...args: unknown[]) => deleteJson(...args),
  putData: vi.fn()
}));

import { createAuthApi } from './authApi';

describe('authApi', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    postJson.mockResolvedValue(ok(undefined));
  });

  it('changeOwnPassword posts to the change-password endpoint', async () => {
    const api = createAuthApi();

    await api.changeOwnPassword('OldPassword1234!', 'NewPassword1234!');

    expect(postJson).toHaveBeenCalledWith('/api/auth/change-password', {
      currentPassword: 'OldPassword1234!',
      newPassword: 'NewPassword1234!'
    });
  });
});
