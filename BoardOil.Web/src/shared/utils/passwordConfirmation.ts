export const PASSWORD_CONFIRMATION_ERROR = 'Passwords do not match.';

export function validatePasswordConfirmation(password: string, confirmPassword: string): string | null {
  return password === confirmPassword ? null : PASSWORD_CONFIRMATION_ERROR;
}
