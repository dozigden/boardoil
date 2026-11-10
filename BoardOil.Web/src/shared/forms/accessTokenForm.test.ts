import { createPinia, setActivePinia } from 'pinia';
import { beforeEach, expect, it, vi } from 'vitest';
import { useValidationStore } from '../stores/validationStore';
import { err, ok } from '../types/result';
import type { CreatedAccessToken } from '../types/authTypes';
import { accessTokenForm } from './accessTokenForm';
import { useForm } from './useForm';

beforeEach(() => setActivePinia(createPinia()));

it('filters defaults to allowed choices and leaves submitted values for server validation and excludes expiry UI state from the request', async () => {
  const submit = vi.fn().mockResolvedValue(err({ message: 'Try again' }));
  const form = useForm(accessTokenForm({ submit, allowedScopes: ['mcp:read'], defaultScopes: ['MCP:READ', 'mcp:read', 'api:system'] }));
  expect(form.draft.value.scopes).toEqual(['mcp:read']);
  form.setValue('name', ' My token ');
  form.setValue('scopes', ['api:system', 'mcp:read']);
  form.setValue('expiry', { nonExpiring: false, days: 45, confirmed: true });
  await form.submit();
  expect(submit).toHaveBeenCalledExactlyOnceWith({ name: ' My token ', scopes: ['api:system', 'mcp:read'], expiresInDays: 45 });
  expect(form.draft.value.expiry.days).toBe(45);
  expect(form.dirty.value).toBe(true);
});

it('validates conditional expiry and retains the full success result without storing the secret in the draft', async () => {
  const created: CreatedAccessToken = {
    token: { id: 1, name: 'Agent', tokenPrefix: 'test', scopes: ['mcp:read'], createdAtUtc: '2026-11-10T00:00:00Z', expiresAtUtc: null, lastUsedAtUtc: null, revokedAtUtc: null },
    plainTextToken: 'test-only-secret'
  };
  const submit = vi.fn().mockResolvedValue(ok(created));
  const form = useForm(accessTokenForm({ submit, allowedScopes: ['mcp:read'], defaultScopes: [] }));
  form.setValue('name', 'Agent');
  form.setValue('expiry', { nonExpiring: true, days: null, confirmed: false });
  await form.submit();
  expect(submit).not.toHaveBeenCalled();
  expect(useValidationStore().getFieldErrors('expiry')).toEqual(['Confirm non-expiring token risk before creating.']);
  form.setValue('scopes', ['mcp:read']);
  form.setValue('expiry', { nonExpiring: true, days: null, confirmed: true });
  expect(await form.submit()).toEqual(ok(created));
  expect(submit).toHaveBeenCalledExactlyOnceWith({ name: 'Agent', scopes: ['mcp:read'], expiresInDays: null });
  expect(JSON.stringify(form.draft.value)).not.toContain(created.plainTextToken);
  expect(useValidationStore().validation).toBeNull();
});

it('submits invalid domain values to the server without turning blank finite expiry into non-expiring', async () => {
  const submit = vi.fn().mockResolvedValue(err({ message: 'Validation failed.', validationErrors: {
    name: ['Token name is required.'], expiresInDays: ['Expiry must be between 1 and 3650 days.'], scopes: ['At least one scope is required.']
  } }));
  const form = useForm(accessTokenForm({ submit, allowedScopes: ['mcp:read'], defaultScopes: [] }));
  form.setValue('expiry', { nonExpiring: false, days: null, confirmed: false });
  await form.submit();
  expect(submit).toHaveBeenCalledExactlyOnceWith({ name: '', scopes: [], expiresInDays: 0 });
  expect(useValidationStore().getFieldErrors('name')).toEqual(['Token name is required.']);
  expect(form.draft.value.expiry.days).toBeNull();
});

it('clears only the corrected compound field aliases and resets expiry on a new draft', async () => {
  const submit = vi.fn().mockResolvedValue(err({ message: 'Validation failed.', validationErrors: {
    scopes: ['Scope unavailable.'], expiresInDays: ['Choose a shorter expiry.']
  } }));
  const form = useForm(accessTokenForm({ submit, allowedScopes: ['mcp:read'], defaultScopes: ['mcp:read'] }));
  form.setValue('name', 'Agent');
  await form.submit();
  form.setValue('expiry', { nonExpiring: false, days: 10, confirmed: false });
  expect(useValidationStore().getFieldErrors('expiresInDays')).toEqual([]);
  expect(useValidationStore().getFieldErrors('scopes')).toEqual(['Scope unavailable.']);
  form.reset();
  expect(form.draft.value.expiry).toEqual({ nonExpiring: false, days: 30, confirmed: false });
  expect(form.draft.value.name).toBe('');
});
