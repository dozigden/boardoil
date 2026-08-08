export type OAuthConnectionOwner = {
  id: number;
  userName: string;
  displayName: string;
};

export type OAuthConnection = {
  id: number;
  name: string;
  resourceType: string;
  owner: OAuthConnectionOwner;
  approvedScopes: string[];
  oAuthClientId: string;
  oAuthClientDisplayName: string;
  resource: string;
  createdAtUtc: string;
  lastAuthorizedAtUtc: string;
  lastUsedAtUtc: string | null;
};

export type OAuthProtectedResourceMetadata = {
  resource: string;
  authorization_servers: string[];
  scopes_supported: string[];
  bearer_methods_supported: string[];
};
