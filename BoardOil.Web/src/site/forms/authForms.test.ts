import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useForm } from '../../shared/forms/useForm';
import { useValidationStore } from '../../shared/stores/validationStore';
import { err } from '../../shared/types/result';
import { createLoginForm } from './loginForm';
import { createInitialAdminForm } from './initialAdminForm';

beforeEach(() => setActivePinia(createPinia()));

describe('auth forms', () => {
  it('sends empty login credentials to the backend and retains its general rejection', async () => {
    const result = err({ message: 'Invalid username or password.' });
    const login = vi.fn().mockResolvedValue(result);
    const form = useForm(createLoginForm(login));

    expect(await form.submit()).toBe(result);
    expect(login).toHaveBeenCalledWith({ userName: '', password: '' });
    expect(useValidationStore().getValidationMessages()).toEqual(['Invalid username or password.']);
  });

  it('validates only password confirmation locally and sends domain validation to the backend', async () => {
    const result = err({ message: 'Validation failed.', validationErrors: { password: ['Password must be at least 10 characters.'] } });
    const register = vi.fn().mockResolvedValue(result);
    const form = useForm(createInitialAdminForm({ defaultId: 'UTC', options: [{ id: 'UTC', displayName: 'UTC' }] }, register));
    form.setValue('password', 'short');
    expect((await form.submit())?.ok).toBe(false);
    expect(register).not.toHaveBeenCalled();
    expect(useValidationStore().getFieldErrors('confirmPassword')).toEqual(['Passwords do not match.']);
    form.setValue('confirmPassword', 'short');

    expect(await form.submit()).toBe(result);
    expect(register).toHaveBeenCalledWith({ userName: '', email: '', password: 'short', systemTimeZoneId: 'UTC' });
    expect(form.draft.value.confirmPassword).toBe('short');
    expect(useValidationStore().getFieldErrors('password')).toEqual(['Password must be at least 10 characters.']);
  });
});
