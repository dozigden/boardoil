import { defineForm, type FormDefinition } from './formDefinition';
import type { CreateAccessTokenRequest, CreatedAccessToken } from '../types/authTypes';
import { allowedTokenScopes, createTokenExpiryDraft, tokenExpiryDays, type TokenExpiryDraft } from '../utils/accessTokenDraft';

type TokenDraft = { name: string; scopes: string[]; expiry: TokenExpiryDraft };
export type AccessTokenFormOptions = {
  allowedScopes: readonly string[];
  defaultScopes: readonly string[];
  submit: FormDefinition<CreateAccessTokenRequest, CreatedAccessToken>['submit'];
};

export function accessTokenForm(options: AccessTokenFormOptions) {
  return defineForm<TokenDraft, CreatedAccessToken>({
    title: 'Create Access Token',
    submitLabel: 'Create token',
    cancelLabel: 'Cancel creation',
    closeLabel: 'Cancel access token creation',
    initialValues: () => ({ name: '', scopes: allowedTokenScopes(options.defaultScopes, options.allowedScopes), expiry: createTokenExpiryDraft() }),
    fields: [
      { name: 'name', label: 'Token name', type: 'text' },
      { name: 'scopes', label: 'Scopes', type: 'tokenScopes', allowedScopes: options.allowedScopes },
      { name: 'expiry', label: 'Expiry', type: 'tokenExpiry', errorFields: ['expiresInDays'] }
    ],
    // Acknowledgement belongs to the UI; persisted-value rules come from the API.
    validate: ({ expiry }) => expiry.nonExpiring && !expiry.confirmed
      ? { expiry: ['Confirm non-expiring token risk before creating.'] }
      : {},
    submit: values => options.submit({ name: values.name, scopes: values.scopes, expiresInDays: tokenExpiryDays(values.expiry) })
  });
}
