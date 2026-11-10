import { defineForm, type FormDefinition, type FormFieldDefinition } from './formDefinition';
import { validatePasswordConfirmation } from '../utils/passwordConfirmation';

type PasswordResetDraft = { currentPassword: string; newPassword: string; confirmPassword: string };
export type PasswordResetRequest = { currentPassword?: string; newPassword: string };

export function createPasswordResetForm(
  mode: 'self' | 'admin',
  save: FormDefinition<PasswordResetRequest, void>['submit']
) {
  const fields: FormFieldDefinition<PasswordResetDraft>[] = [];
  if (mode === 'self') {
    fields.push({ name: 'currentPassword', label: 'Current password', type: 'password', autocomplete: 'current-password' });
  }
  fields.push(
    { name: 'newPassword', label: 'New password', type: 'password', autocomplete: 'new-password', clearFields: ['confirmPassword'] },
    { name: 'confirmPassword', label: 'Confirm new password', type: 'password', autocomplete: 'new-password' }
  );
  return defineForm<PasswordResetDraft, void>({
    title: mode === 'self' ? 'Reset Password' : 'Reset User Password',
    submitLabel: 'Reset password',
    cancelLabel: 'Cancel password reset',
    initialValues: () => ({ currentPassword: '', newPassword: '', confirmPassword: '' }),
    fields,
    validate(values) {
      const error = validatePasswordConfirmation(values.newPassword, values.confirmPassword);
      return error ? { confirmPassword: [error] } : {};
    },
    submit: ({ currentPassword, newPassword }) => save({
      currentPassword: mode === 'self' ? currentPassword : undefined,
      newPassword
    })
  });
}
