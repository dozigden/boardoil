import { createPinia, setActivePinia } from 'pinia';
import { expect, it, vi } from 'vitest';
import { useForm } from '../../shared/forms/useForm';
import { err } from '../../shared/types/result';
import { createUserForm } from './createUserForm';

it('submits normalized user data, including the selected role, without the local confirmation field', async () => {
  setActivePinia(createPinia());
  const save = vi.fn().mockResolvedValue(err({ message: 'Retain the draft' }));
  const form = useForm(createUserForm(save));
  form.setValue('userName', ' user ');
  form.setValue('displayName', ' Example User ');
  form.setValue('email', ' user@example.test ');
  form.setValue('password', ' Password1234! ');
  form.setValue('confirmPassword', ' Password1234! ');
  form.setValue('role', 'Admin');
  await form.submit();
  expect(save).toHaveBeenCalledExactlyOnceWith({
    userName: 'user', displayName: 'Example User', email: 'user@example.test',
    password: ' Password1234! ', role: 'Admin'
  });
  expect(form.draft.value.confirmPassword).toBe(' Password1234! ');
});
