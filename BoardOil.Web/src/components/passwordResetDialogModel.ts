import { validatePasswordConfirmation } from '../utils/passwordConfirmation';

export type PasswordResetMode = 'self' | 'admin';

export function validatePasswordResetDraft(
  mode: PasswordResetMode,
  currentPassword: string,
  newPassword: string,
  confirmPassword: string
): string | null {
  if (mode === 'self' && !currentPassword.trim()) {
    return 'Current password is required.';
  }

  return validatePasswordConfirmation(newPassword, confirmPassword);
}
