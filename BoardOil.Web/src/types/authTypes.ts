export type AuthUser = {
  id: number;
  userName: string;
  role: 'Admin' | 'Standard' | string;
};

export type AuthSession = {
  user: AuthUser;
  accessTokenExpiresAtUtc: string;
  refreshTokenExpiresAtUtc: string;
  csrfToken: string;
};

export type CsrfTokenDto = {
  csrfToken: string;
};
