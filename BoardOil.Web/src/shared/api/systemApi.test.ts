import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ok } from '../types/result';

const putJson = vi.fn();

vi.mock('./http', () => ({
  deleteJson: vi.fn(),
  getEnvelope: vi.fn(),
  patchData: vi.fn(),
  postData: vi.fn(),
  putData: vi.fn(),
  putJson: (...args: unknown[]) => putJson(...args)
}));

import { createSystemApi } from './systemApi';

describe('systemApi', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    putJson.mockResolvedValue(ok(undefined));
  });

  it('resetUserPassword puts to the system user password endpoint', async () => {
    const api = createSystemApi();

    await api.resetUserPassword(42, 'FreshPassword1234!');

    expect(putJson).toHaveBeenCalledWith('/api/system/users/42/password', {
      newPassword: 'FreshPassword1234!'
    });
  });
});
