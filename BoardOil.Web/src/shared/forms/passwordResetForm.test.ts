import { createPinia, setActivePinia } from 'pinia';
import { expect, it, vi } from 'vitest';
import { useValidationStore } from '../stores/validationStore';
import { err, ok } from '../types/result';
import { createPasswordResetForm } from './passwordResetForm';
import { useForm } from './useForm';

it('keeps confirmation local while letting the backend reject empty password fields', async () => {
  setActivePinia(createPinia());
  const failure = err({ message: 'Validation failed.', validationErrors: { currentPassword: ['Current password is required.'] } });
  const save = vi.fn().mockResolvedValueOnce(failure).mockResolvedValueOnce(ok(undefined));
  const form = useForm(createPasswordResetForm('self', save));
  const validation = useValidationStore();
  form.setValue('newPassword', 'new-password');
  await form.submit();
  expect(save).not.toHaveBeenCalled();
  expect(validation.getFieldErrors('confirmPassword')).toEqual(['Passwords do not match.']);

  form.setValue('confirmPassword', 'new-password');
  await form.submit();
  expect(save).toHaveBeenCalledWith({ currentPassword: '', newPassword: 'new-password' });
  expect(validation.getFieldErrors('currentPassword')).toEqual(['Current password is required.']);
  expect(form.draft.value.newPassword).toBe('new-password');
  form.setValue('currentPassword', 'old-password');
  expect(validation.getFieldErrors('currentPassword')).toEqual([]);
  await form.submit();
  expect(form.dirty.value).toBe(false);
});
