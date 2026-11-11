import { defineForm, type FormDefinition } from '../../shared/forms/formDefinition';
import type { AuthUser } from '../../shared/types/authTypes';
import type { SystemTimeZoneOptions } from '../../shared/types/scheduledJobTypes';
import { validatePasswordConfirmation } from '../../shared/utils/passwordConfirmation';

type InitialAdminRequest = {
  userName: string;
  email: string;
  password: string;
  systemTimeZoneId: string
};
type InitialAdminDraft = InitialAdminRequest & { confirmPassword: string };

export function createInitialAdminForm(timeZones: SystemTimeZoneOptions, register: FormDefinition<InitialAdminRequest, AuthUser>['submit']) {
  return defineForm<InitialAdminDraft, AuthUser>({
    title: 'Create Initial Admin',
    submitLabel: 'Create admin',
    initialValues: () => ({
      userName: '',
      email: '',
      password: '',
      confirmPassword: '',
      systemTimeZoneId: timeZones.defaultId
    }),
    fields: [
      {
        name: 'userName',
        label: 'Username',
        type: 'text',
        autocomplete: 'username'
      },
      {
        name: 'email',
        label: 'Email',
        type: 'email',
        autocomplete: 'email'
      },
      {
        name: 'password',
        label: 'Password',
        type: 'password',
        autocomplete: 'new-password',
        clearFields: ['confirmPassword']
      },
      {
        name: 'confirmPassword',
        label: 'Confirm password',
        type: 'password',
        autocomplete: 'new-password'
      },
      {
        name: 'systemTimeZoneId',
        label: 'System timezone',
        type: 'searchableSelect',
        options: timeZones.options.map(option => ({ value: option.id, label: option.displayName }))
      }
    ],
    validate({ password, confirmPassword }) {
      const error = validatePasswordConfirmation(password, confirmPassword);
      return error ? { confirmPassword: [error] } : {};
    },
    submit: ({ userName, email, password, systemTimeZoneId }) => register({
      userName,
      email,
      password,
      systemTimeZoneId
    })
  });
}
