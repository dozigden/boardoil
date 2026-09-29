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

  it('returns the token and its authenticated user together', async () => {
    const api = createAuthApi();
    getEnvelope.mockResolvedValue(ok({
      success: true, data: { csrfToken: 'bound-token', userId: 42 }, statusCode: 200
    }));

    const result = await api.getCsrfToken();

    expect(result).toEqual(ok({ csrfToken: 'bound-token', userId: 42 }));
  });

  it('changeOwnPassword posts to the change-password endpoint', async () => {
    const api = createAuthApi();

    await api.changeOwnPassword('OldPassword1234!', 'NewPassword1234!');

    expect(postJson).toHaveBeenCalledWith('/api/auth/change-password', {
      currentPassword: 'OldPassword1234!',
      newPassword: 'NewPassword1234!'
    });
  });

  it('includes the selected system timezone in initial admin registration', async () => {
    const api = createAuthApi();

    await api.registerInitialAdmin('admin', 'admin@example.test', 'Password1234!', 'Europe/London');

    expect(postData).toHaveBeenCalledWith('/api/auth/register-initial-admin', {
      userName: 'admin', email: 'admin@example.test', password: 'Password1234!', systemTimeZoneId: 'Europe/London'
    });
  });

  it('loads the timezone catalogue through the initial setup endpoint', async () => {
    const api = createAuthApi();
    const options = { defaultId: 'UTC', options: [{ id: 'UTC', displayName: 'UTC [UTC+00:00]' }] };
    getEnvelope.mockResolvedValue(ok({ success: true, statusCode: 200, data: options }));

    const result = await api.getTimeZoneOptions();

    expect(getEnvelope).toHaveBeenCalledWith('/api/auth/timezone-options');
    expect(result).toEqual(ok(options));
  });

});
