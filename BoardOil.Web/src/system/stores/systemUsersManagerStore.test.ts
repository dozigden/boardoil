import { createPinia, setActivePinia } from 'pinia';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createSystemApi, type SystemApi } from '../../shared/api/systemApi';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import { useValidationStore } from '../../shared/stores/validationStore';
import type { AppError } from '../../shared/types/appError';
import type { Result } from '../../shared/types/result';
import type { ManagedUser } from '../../shared/types/authTypes';
import { err, ok } from '../../shared/types/result';

vi.mock('../../shared/api/systemApi', () => ({
  createSystemApi: vi.fn()
}));

import { useSystemUsersManagerStore } from './systemUsersManagerStore';

describe('systemUsersManagerStore feedback', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.resetAllMocks();
  });

  it('shows successful mutations as toasts', async () => {
    const api = createApi();
    api.createUser.mockResolvedValue(ok(newUser('New User')));
    vi.mocked(createSystemApi).mockReturnValue(api);
    const store = useSystemUsersManagerStore();
    const feedback = useUiFeedbackStore();

    const created = await store.createUser({
      userName: 'new-user',
      displayName: 'New User',
      email: 'new-user@example.test',
      password: 'Password1234!',
      role: 'Standard'
    });

    expect(created).toEqual(ok(newUser('New User')));
    expect(feedback.toastMessage).toBe('Created successfully.');
    expect(feedback.toastTone).toBe('success');
    expect(store.errorMessage).toBeNull();
  });

  it('returns full creation results without publishing validation or a failure toast', async () => {
    const api = createApi();
    api.createUser.mockResolvedValueOnce(err({
      kind: 'api', message: 'Validation failed.', validationErrors: { password: ['Too short'] }
    }));
    vi.mocked(createSystemApi).mockReturnValue(api);
    const store = useSystemUsersManagerStore();
    const validation = useValidationStore();
    const feedback = useUiFeedbackStore();
    const payload = { userName: 'new-user', displayName: 'New User', email: 'new-user@example.test', password: 'short', role: 'Standard' as const };
    store.errorMessage = 'Existing load error';

    const existingValidation = err({ message: 'Another form error' });
    validation.setValidationResult(existingValidation);
    expect(await store.createUser(payload)).toEqual(err({
      kind: 'api', message: 'Validation failed.', validationErrors: { password: ['Too short'] }
    }));
    expect(validation.getValidationMessages()).toEqual(['Another form error']);
    expect(feedback.toastMessage).toBe('');
    expect(store.errorMessage).toBe('Existing load error');
    expect(store.users).toEqual([]);
    expect(store.busy).toBe(false);

    vi.mocked(api.getUsers).mockResolvedValueOnce(ok([]));
    await store.loadUsers();
    expect(validation.getValidationMessages()).toEqual(['Another form error']);

    let finish!: (result: Result<ManagedUser, AppError>) => void;
    api.createUser.mockReturnValueOnce(new Promise<Result<ManagedUser, AppError>>(resolve => { finish = resolve; }));
    const retry = store.createUser({ ...payload, password: 'Password1234!' });
    expect(validation.getValidationMessages()).toEqual(['Another form error']);
    expect(store.busy).toBe(true);
    finish(ok(newUser('New User')));
    expect(await retry).toEqual(ok(newUser('New User')));
    expect(store.busy).toBe(false);
    expect(store.users).toEqual([newUser('New User')]);
    expect(validation.getValidationMessages()).toEqual(['Another form error']);
    expect(feedback.toastMessage).toBe('Created successfully.');
  });

  it('returns edit and reset errors unchanged, preserving users and leaving feedback to the form', async () => {
    const api = createApi();
    const failure = err({ kind: 'api' as const, message: 'Validation failed.', validationErrors: { email: ['Email already exists.'] } });
    vi.mocked(api.updateUser).mockResolvedValueOnce(failure);
    vi.mocked(api.resetUserPassword).mockResolvedValueOnce(failure);
    vi.mocked(createSystemApi).mockReturnValue(api);
    const store = useSystemUsersManagerStore();
    const user = newUser('Original');
    store.users = [user];
    store.errorMessage = 'Existing load error';
    const request = { displayName: 'Edited', email: 'changed@example.test', role: 'Standard' as const, isActive: false };
    const feedback = useUiFeedbackStore();
    const validation = useValidationStore();
    validation.setValidationResult(err({ message: 'Another form' }));

    expect(await store.updateUser(user.id, request)).toEqual(failure);
    expect(await store.resetUserPassword(user.id, 'short')).toEqual(failure);
    expect(store.users).toEqual([user]);
    expect(store.busy).toBe(false);
    expect(store.errorMessage).toBe('Existing load error');
    expect(feedback.toastMessage).toBe('');
    expect(validation.getValidationMessages()).toEqual(['Another form']);

    const updated = { ...user, ...request };
    vi.mocked(api.updateUser).mockResolvedValueOnce(ok(updated));
    expect(await store.updateUser(user.id, request)).toEqual(ok(updated));
    expect(store.users).toEqual([updated]);
    vi.mocked(api.resetUserPassword).mockResolvedValueOnce(ok(undefined));
    expect(await store.resetUserPassword(user.id, 'Password1234!')).toEqual(ok(undefined));
    expect(store.busy).toBe(false);
  });

  it('shows mutation failures as error toasts without replacing load errors', async () => {
    const api = createApi();
    api.deleteUser.mockResolvedValue(err({ kind: 'api', message: 'Could not delete user.' }));
    vi.mocked(createSystemApi).mockReturnValue(api);
    const store = useSystemUsersManagerStore();
    const feedback = useUiFeedbackStore();

    const deleted = await store.deleteUser(1);

    expect(deleted).toBe(false);
    expect(feedback.toastMessage).toBe('Could not delete user.');
    expect(feedback.toastTone).toBe('error');
    expect(store.errorMessage).toBeNull();
  });
});

function createApi() {
  return {
    createUser: vi.fn(),
    deleteUser: vi.fn(),
    getUsers: vi.fn(),
    resetUserPassword: vi.fn(),
    updateUser: vi.fn()
  } as unknown as SystemApi & {
    createUser: ReturnType<typeof vi.fn>;
    deleteUser: ReturnType<typeof vi.fn>;
  };
}

function newUser(displayName: string): ManagedUser {
  return {
    id: 1,
    userName: 'new-user',
    displayName,
    email: 'new-user@example.test',
    role: 'Standard',
    identityType: 'User',
    isActive: true,
    createdAtUtc: '2026-08-19T12:00:00Z',
    updatedAtUtc: '2026-08-19T12:00:00Z'
  };
}
