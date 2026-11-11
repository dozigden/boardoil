import { defineForm, type FormDefinition } from '../../shared/forms/formDefinition';
import type { CreateClientAccountRequest, CreatedClientAccount } from '../../shared/types/authTypes';
import { createTokenExpiryDraft, tokenExpiryDays, tokenScopeDefinitions, type TokenExpiryDraft } from '../../shared/utils/accessTokenDraft';

type ClientAccountDraft = {
  userName: string;
  displayName: string;
  email: string;
  role: string;
  tokenName: string;
  scopes: string[];
  expiry: TokenExpiryDraft;
};

export function createClientAccountForm(save: FormDefinition<CreateClientAccountRequest, CreatedClientAccount>['submit']) {
  return defineForm<ClientAccountDraft, CreatedClientAccount>({
    title: 'Create Client Account',
    submitLabel: 'Create client account',
    cancelLabel: 'Cancel creation',
    closeLabel: 'Cancel client account creation',
    initialValues: () => ({
      userName: '',
      displayName: '',
      email: '',
      role: 'Standard',
      tokenName: 'Initial token',
      scopes: ['api:read', 'api:write', 'api:admin', 'api:system'],
      expiry: createTokenExpiryDraft()
    }),
    fields: [
      {
        name: 'userName',
        label: 'Username',
        type: 'text'
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
        name: 'tokenName',
        label: 'Token name',
        type: 'text'
      },
      {
        name: 'scopes',
        label: 'Scopes',
        type: 'tokenScopes',
        allowedScopes: tokenScopeDefinitions.map(scope => scope.id)
      },
      {
        name: 'expiry',
        label: 'Expiry',
        type: 'tokenExpiry',
        errorFields: ['expiresInDays']
      }
    ],
    validate: ({ expiry }) => expiry.nonExpiring && !expiry.confirmed
      ? { expiry: ['Confirm non-expiring token risk before creating.'] }
      : {},
    submit: ({ expiry, ...values }) => save({ ...values, expiresInDays: tokenExpiryDays(expiry) })
  });
}
