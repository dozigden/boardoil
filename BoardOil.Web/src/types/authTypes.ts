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

export type BootstrapStatusDto = {
  requiresInitialAdminSetup: boolean;
};

export type ManagedUser = {
  id: number;
  userName: string;
  role: 'Admin' | 'Standard' | string;
  identityType: 'User' | 'Client' | string;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
};

export type ClientAccount = {
  id: number;
  userName: string;
  role: 'Admin' | 'Standard' | string;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
};

export type UserDirectoryEntry = {
  id: number;
  userName: string;
  isActive: boolean;
};

export type AccessTokenScope = 'mcp:read' | 'mcp:write' | 'api:read' | 'api:write' | 'api:admin' | 'api:system' | string;

export type AccessTokenBoardAccessMode = 'all' | 'selected' | string;

export type AccessToken = {
  id: number;
  name: string;
  tokenPrefix: string;
  scopes: AccessTokenScope[];
  boardAccessMode: AccessTokenBoardAccessMode;
  allowedBoardIds: number[];
  createdAtUtc: string;
  expiresAtUtc: string | null;
  lastUsedAtUtc: string | null;
  revokedAtUtc: string | null;
};

export type CreatedAccessToken = {
  token: AccessToken;
  plainTextToken: string;
};

export type CreateAccessTokenRequest = {
  name: string;
  expiresInDays: number | null;
  scopes: AccessTokenScope[];
  boardAccessMode: AccessTokenBoardAccessMode;
  allowedBoardIds: number[];
};

export type CreateClientAccountRequest = {
  userName: string;
  role: 'Admin' | 'Standard' | string;
  tokenName?: string | null;
  expiresInDays?: number | null;
  scopes?: AccessTokenScope[] | null;
  boardAccessMode?: AccessTokenBoardAccessMode;
  allowedBoardIds?: number[];
};

export type CreatedClientAccount = {
  account: ClientAccount;
  token: CreatedAccessToken;
};
