import { defineForm, type FormDefinition, type FormErrors } from '../../shared/forms/formDefinition';
import type { CreateManagedUserRequest, ManagedUser } from '../../shared/types/authTypes';
import { validatePasswordConfirmation } from '../../shared/utils/passwordConfirmation';

type UserDraft = CreateManagedUserRequest & { confirmPassword: string };

export function createUserForm(save: FormDefinition<CreateManagedUserRequest, ManagedUser>['submit']) {
  return defineForm<UserDraft, ManagedUser>({
    title: 'Create User',
    submitLabel: 'Create user',
    cancelLabel: 'Cancel creation',
    initialValues: () => ({ userName: '', displayName: '', email: '', password: '', confirmPassword: '', role: 'Standard' }),
    fields: [
      { name: 'userName', label: 'Username', type: 'text', required: true, maxLength: 64 },
      { name: 'displayName', label: 'Display name', type: 'text', required: true, maxLength: 64 },
      { name: 'email', label: 'Email', type: 'email', required: true, maxLength: 320, autocomplete: 'email' },
      { name: 'password', label: 'Password', type: 'password', required: true, autocomplete: 'new-password', clearFields: ['confirmPassword'] },
      { name: 'confirmPassword', label: 'Confirm password', type: 'password', required: true, autocomplete: 'new-password' },
      { name: 'role', label: 'Role', type: 'select', options: [{ value: 'Standard', label: 'Standard' }, { value: 'Admin', label: 'Admin' }] }
    ],
    normalize: values => ({ ...values, userName: values.userName.trim(), displayName: values.displayName.trim(), email: values.email.trim() }),
    validate(values) {
      const errors: FormErrors<UserDraft> = {};
      const atIndex = values.email.indexOf('@');
      if (atIndex <= 0 || atIndex !== values.email.lastIndexOf('@') || atIndex >= values.email.length - 1) {
        errors.email = ["Email must contain '@' with characters before and after it."];
      }
      const passwordError = validatePasswordConfirmation(values.password, values.confirmPassword);
      if (passwordError) errors.confirmPassword = [passwordError];
      if (!values.displayName) errors.displayName = ['Display name is required.'];
      return errors;
    },
    submit: ({ userName, displayName, email, password, role }) => save({ userName, displayName, email, password, role })
  });
}
