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

export type ManagedUser = {
  id: number;
  userName: string;
  role: 'Admin' | 'Standard' | string;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
};
