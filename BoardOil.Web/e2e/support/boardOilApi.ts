import type { APIRequestContext, APIResponse } from '@playwright/test';

type ApiEnvelope<T> = {
  success: boolean;
  data: T | null;
  message?: string;
};

export type SmokeBoard = {
  id: number;
  columns: Array<{
    id: number;
    title: string;
  }>;
};

type SmokeCardType = {
  id: number;
  isSystem: boolean;
};

export type SmokeCard = {
  id: number;
  boardColumnId: number;
  cardTypeId: number;
  title: string;
  description: string;
  tagNames: string[];
};

export type SmokeOAuthClient = {
  authorizationServer: string;
  clientId: string;
  redirectUri: string;
  resource: string;
};

export class BoardOilApi {
  private csrfToken = '';

  public constructor(private readonly request: APIRequestContext) {}

  public async ensureInitialAdmin(userName: string, password: string) {
    const bootstrap = await this.read<{ requiresInitialAdminSetup: boolean }>('/api/auth/bootstrap-status');
    let session: { csrfToken: string };

    if (bootstrap.requiresInitialAdminSetup) {
      session = await this.post<{ csrfToken: string }>('/api/auth/register-initial-admin', {
        userName,
        email: 'smoke-admin@boardoil.test',
        password
      }, false);
    } else {
      session = await this.post<{ csrfToken: string }>('/api/auth/login', {
        userName,
        password
      }, false);
    }

    this.csrfToken = session.csrfToken;
  }

  public async createBoard(name: string) {
    return await this.post<SmokeBoard>('/api/boards', {
      name,
      description: 'Playwright smoke test board'
    });
  }

  public async createUser(userName: string, password: string) {
    return await this.post<{ id: number; userName: string }>('/api/system/users', {
      userName,
      displayName: userName,
      email: `${userName}@boardoil.test`,
      password,
      role: 'Standard'
    });
  }

  public async registerOAuthClient(
    clientName: string,
    redirectUri: string
  ): Promise<SmokeOAuthClient> {
    const metadataResponse = await this.request.get(
      '/.well-known/oauth-protected-resource/mcp/oauth'
    );
    const metadata = await readJson<{
      authorization_servers: string[];
      resource: string;
    }>(metadataResponse);
    const discoveredAuthorizationServer = metadata.authorization_servers[0];
    if (!discoveredAuthorizationServer) {
      throw new Error('OAuth metadata did not contain an authorization server.');
    }
    const authorizationServer = discoveredAuthorizationServer.replace(/\/+$/, '');

    const registrationResponse = await this.request.post(
      `${authorizationServer}/connect/register`,
      {
        data: {
          client_name: clientName,
          redirect_uris: [redirectUri],
          grant_types: ['authorization_code', 'refresh_token'],
          response_types: ['code'],
          token_endpoint_auth_method: 'none',
          scope: 'mcp:read',
          application_type: 'native'
        }
      }
    );
    const registration = await readJson<{ client_id: string }>(registrationResponse);
    return {
      authorizationServer,
      clientId: registration.client_id,
      redirectUri,
      resource: metadata.resource
    };
  }

  public async createCard(
    board: SmokeBoard,
    columnTitle: string,
    title: string,
    description = '',
    tagNames: string[] = []
  ) {
    const column = board.columns.find(candidate => candidate.title === columnTitle);
    if (!column) {
      throw new Error(`Column '${columnTitle}' was not found on board ${board.id}.`);
    }

    const cardTypes = await this.read<SmokeCardType[]>(`/api/boards/${board.id}/card-types`);
    const cardType = cardTypes.find(candidate => candidate.isSystem) ?? cardTypes[0];
    if (!cardType) {
      throw new Error(`No card type was found on board ${board.id}.`);
    }

    return await this.post<SmokeCard>(`/api/boards/${board.id}/cards`, {
      boardColumnId: column.id,
      title,
      description,
      tagNames,
      cardTypeId: cardType.id,
      assignedUserId: null,
      slickName: null,
      externalUrl: null
    });
  }

  private async read<T>(path: string) {
    const response = await this.request.get(path);
    return await readEnvelope<T>(response);
  }

  private async post<T>(path: string, data: unknown, includeCsrf = true) {
    const headers: Record<string, string> = {};
    if (includeCsrf) {
      headers['X-BoardOil-CSRF'] = this.csrfToken;
    }

    const response = await this.request.post(path, { data, headers });
    return await readEnvelope<T>(response);
  }
}

async function readEnvelope<T>(response: APIResponse) {
  const bodyText = await response.text();
  let envelope: ApiEnvelope<T> | null = null;

  try {
    envelope = JSON.parse(bodyText) as ApiEnvelope<T>;
  } catch {
    // The error below includes the non-JSON response body.
  }

  if (!response.ok() || envelope?.success === false || envelope?.data === null || envelope?.data === undefined) {
    const message = envelope?.message ?? bodyText;
    throw new Error(`${response.url()} failed (${response.status()}): ${message}`);
  }

  return envelope.data;
}

async function readJson<T>(response: APIResponse) {
  const bodyText = await response.text();
  if (!response.ok()) {
    throw new Error(`${response.url()} failed (${response.status()}): ${bodyText}`);
  }

  try {
    return JSON.parse(bodyText) as T;
  } catch {
    throw new Error(`${response.url()} did not return valid JSON: ${bodyText}`);
  }
}
