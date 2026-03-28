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
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
};

export type MachinePatScope = 'mcp:read' | 'mcp:write' | string;

export type MachinePatBoardAccessMode = 'all' | 'selected' | string;

export type MachinePat = {
  id: number;
  name: string;
  tokenPrefix: string;
  scopes: MachinePatScope[];
  boardAccessMode: MachinePatBoardAccessMode;
  allowedBoardIds: number[];
  createdAtUtc: string;
  expiresAtUtc: string | null;
  lastUsedAtUtc: string | null;
  revokedAtUtc: string | null;
};

export type CreatedMachinePat = {
  token: MachinePat;
  plainTextToken: string;
};

export type CreateMachinePatRequest = {
  name: string;
  expiresInDays: number | null;
  scopes: MachinePatScope[];
  boardAccessMode: MachinePatBoardAccessMode;
  allowedBoardIds: number[];
};
