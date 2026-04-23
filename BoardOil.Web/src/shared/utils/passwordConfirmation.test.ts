import { describe, expect, it } from 'vitest';
import { PASSWORD_CONFIRMATION_ERROR, validatePasswordConfirmation } from './passwordConfirmation';

describe('passwordConfirmation', () => {
  it('accepts matching passwords', () => {
    expect(validatePasswordConfirmation('Password1234!', 'Password1234!')).toBeNull();
  });

  it('rejects mismatched passwords with a clear message', () => {
    expect(validatePasswordConfirmation('Password1234!', 'Password12345!')).toBe(PASSWORD_CONFIRMATION_ERROR);
  });
});
