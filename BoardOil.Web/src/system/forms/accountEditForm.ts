import { defineForm, type FormDefinition } from '../../shared/forms/formDefinition';
import type { UpdateManagedUserRequest } from '../../shared/types/authTypes';

type AccountDraft = UpdateManagedUserRequest & { userName: string };

export function createAccountEditForm<R>(
  account: AccountDraft,
  kind: 'user' | 'client',
  save: FormDefinition<UpdateManagedUserRequest, R>['submit']
) {
  return defineForm<AccountDraft, R>({
    title: kind === 'user' ? 'Edit User' : 'Edit Client Account',
    submitLabel: kind === 'user' ? 'Save user changes' : 'Save client account changes',
    submitText: 'Save',
    cancelLabel: kind === 'user' ? 'Cancel user changes' : 'Cancel client changes',
    initialValues: () => ({
      userName: account.userName,
      displayName: account.displayName,
      email: account.email,
      role: account.role,
      isActive: account.isActive
    }),
    fields: [
      {
        name: 'userName',
        label: 'Username',
        type: 'text',
        disabled: () => true
      },
      {
        name: 'displayName',
        label: 'Display name',
        type: 'text'
      },
      {
        name: 'email',
        label: 'Email',
        type: 'email',
        autocomplete: 'email'
      },
      {
        name: 'role',
        label: 'Role',
        type: 'select',
        options: [{ value: 'Standard', label: 'Standard' }, { value: 'Admin', label: 'Admin' }]
      },
      {
        name: 'isActive',
        label: 'Active account',
        type: 'checkbox'
      }
    ],
    submit: ({ displayName, email, role, isActive }) => save({
      displayName,
      email,
      role,
      isActive
    })
  });
}
