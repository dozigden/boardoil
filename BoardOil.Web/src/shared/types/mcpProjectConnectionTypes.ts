export type McpProjectConnectionScope = 'mcp:read' | 'mcp:write';

export type McpProjectConnection = {
  id: number;
  publicId: string;
  name: string;
  clientAccountId: number;
  clientAccountUserName: string;
  clientAccountDisplayName: string;
  allowedScopes: McpProjectConnectionScope[];
  resourceUrl: string;
  isActive: boolean;
  createdByUserId: number | null;
  createdByUserName: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  revokedAtUtc: string | null;
  revokedByUserId: number | null;
  revokedByUserName: string | null;
};

export type CreateMcpProjectConnectionRequest = {
  clientAccountId: number;
  name: string;
  allowedScopes: McpProjectConnectionScope[];
};
