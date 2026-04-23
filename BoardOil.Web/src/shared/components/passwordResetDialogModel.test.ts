import { describe, expect, it } from 'vitest';
import { PASSWORD_CONFIRMATION_ERROR } from '../utils/passwordConfirmation';
import { validatePasswordResetDraft } from './passwordResetDialogModel';

describe('passwordResetDialogModel', () => {
  it('requires current password in self mode', () => {
    const result = validatePasswordResetDraft('self', '', 'Password1234!', 'Password1234!');

    expect(result).toBe('Current password is required.');
  });

  it('allows empty current password in admin mode', () => {
    const result = validatePasswordResetDraft('admin', '', 'Password1234!', 'Password1234!');

    expect(result).toBeNull();
  });

  it('rejects mismatched new password and confirmation', () => {
    const result = validatePasswordResetDraft('admin', '', 'Password1234!', 'Password9876!');

    expect(result).toBe(PASSWORD_CONFIRMATION_ERROR);
  });

  it('accepts matching new password and confirmation', () => {
    const result = validatePasswordResetDraft('self', 'CurrentPassword1234!', 'Password1234!', 'Password1234!');

    expect(result).toBeNull();
  });
});
