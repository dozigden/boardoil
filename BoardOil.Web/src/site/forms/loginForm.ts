import { defineForm, type FormDefinition } from '../../shared/forms/formDefinition';
import type { AuthUser } from '../../shared/types/authTypes';

type LoginDraft = { userName: string; password: string };

export function createLoginForm(login: FormDefinition<LoginDraft, AuthUser>['submit']) {
  return defineForm<LoginDraft, AuthUser>({
    title: 'Sign in',
    submitLabel: 'Login',
    initialValues: () => ({ userName: '', password: '' }),
    fields: [
      {
        name: 'userName',
        label: 'Username',
        type: 'text',
        autocomplete: 'username'
      },
      {
        name: 'password',
        label: 'Password',
        type: 'password',
        autocomplete: 'current-password'
      }
    ],
    submit: login
  });
}
