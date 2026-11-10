import { defineForm, type FormDefinition } from '../../shared/forms/formDefinition';
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
      { name: 'userName', label: 'Username', type: 'text' },
      { name: 'displayName', label: 'Display name', type: 'text' },
      { name: 'email', label: 'Email', type: 'email', autocomplete: 'email' },
      { name: 'password', label: 'Password', type: 'password', autocomplete: 'new-password', clearFields: ['confirmPassword'] },
      { name: 'confirmPassword', label: 'Confirm password', type: 'password', autocomplete: 'new-password' },
      { name: 'role', label: 'Role', type: 'select', options: [{ value: 'Standard', label: 'Standard' }, { value: 'Admin', label: 'Admin' }] }
    ],
    validate(values) {
      const error = validatePasswordConfirmation(values.password, values.confirmPassword);
      return error ? { confirmPassword: [error] } : {};
    },
    submit: ({ userName, displayName, email, password, role }) => save({ userName, displayName, email, password, role })
  });
}
