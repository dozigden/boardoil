import { createPinia, setActivePinia } from 'pinia';
import { expect, it, vi } from 'vitest';
import { createSystemApi, type SystemApi } from '../../shared/api/systemApi';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import { useValidationStore } from '../../shared/stores/validationStore';
import type { CreatedClientAccount } from '../../shared/types/authTypes';
import { err, ok } from '../../shared/types/result';
import { useSystemClientAccountsStore } from './systemClientAccountsStore';

vi.mock('../../shared/api/systemApi', () => ({ createSystemApi: vi.fn() }));

it('returns complete creation results while keeping submission errors and secrets out of shared stores', async () => {
  setActivePinia(createPinia());
  const rejected = err({ kind: 'api' as const, message: 'Validation failed.', validationErrors: { tokenName: ['Too long'] } });
  const createClientAccount = vi.fn().mockResolvedValueOnce(rejected);
  vi.mocked(createSystemApi).mockReturnValue({ createClientAccount } as unknown as SystemApi);
  const store = useSystemClientAccountsStore();
  const feedback = useUiFeedbackStore();
  const validation = useValidationStore();
  validation.setValidationResult(err({ message: 'Another form' }));
  store.errorMessage = 'Existing load failure';
  const request = { userName: 'bot', displayName: 'Bot', email: 'bot@example.test', role: 'Standard' };

  expect(await store.createClientAccount(request)).toEqual(rejected);
  expect(store.clients).toEqual([]);
  expect(store.busy).toBe(false);
  expect(store.errorMessage).toBe('Existing load failure');
  expect(feedback.toastMessage).toBe('');
  expect(validation.getValidationMessages()).toEqual(['Another form']);

  const created: CreatedClientAccount = {
    account: { id: 3, ...request, isActive: true, createdAtUtc: '2026-11-10T12:00:00Z', updatedAtUtc: '2026-11-10T12:00:00Z' },
    token: {
      plainTextToken: 'one-time-secret',
      token: { id: 4, name: 'Initial token', tokenPrefix: 'prefix', scopes: ['api:read'], createdAtUtc: '2026-11-10T12:00:00Z', expiresAtUtc: null, lastUsedAtUtc: null, revokedAtUtc: null }
    }
  };
  createClientAccount.mockResolvedValueOnce(ok(created));
  expect(await store.createClientAccount(request)).toEqual(ok(created));
  expect(store.clients).toEqual([created.account]);
  expect(JSON.stringify(store.$state)).not.toContain('one-time-secret');
  expect(feedback.toastMessage).toBe('Created successfully.');
  expect(validation.getValidationMessages()).toEqual(['Another form']);
  expect(store.busy).toBe(false);
});

it('returns edit failures without toast duplication and applies successful response values', async () => {
  setActivePinia(createPinia());
  const failure = err({ kind: 'api' as const, message: 'Validation failed.', validationErrors: { email: ['Email already exists.'] } });
  const updateClientAccount = vi.fn().mockResolvedValueOnce(failure);
  vi.mocked(createSystemApi).mockReturnValue({ updateClientAccount } as unknown as SystemApi);
  const store = useSystemClientAccountsStore();
  const client = { id: 3, userName: 'bot', displayName: 'Bot', email: 'bot@example.test', role: 'Standard', isActive: true, createdAtUtc: '', updatedAtUtc: '' };
  store.clients = [client];
  const request = { displayName: 'Changed', email: 'changed@example.test', role: 'Admin', isActive: false };
  const feedback = useUiFeedbackStore();
  const validation = useValidationStore();
  validation.setValidationResult(err({ message: 'Another form' }));
  store.errorMessage = 'Existing load error';

  expect(await store.updateClientAccount(3, request)).toEqual(failure);
  expect(store.clients).toEqual([client]);
  expect(store.busy).toBe(false);
  expect(feedback.toastMessage).toBe('');
  expect(store.errorMessage).toBe('Existing load error');
  expect(validation.getValidationMessages()).toEqual(['Another form']);

  const updated = { ...client, ...request };
  updateClientAccount.mockResolvedValueOnce(ok(updated));
  expect(await store.updateClientAccount(3, request)).toEqual(ok(updated));
  expect(store.clients).toEqual([updated]);
  expect(store.busy).toBe(false);
});
