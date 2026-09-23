import {
  DefaultHttpClient,
  HttpError,
  NullLogger,
  type HttpRequest,
  type IRetryPolicy
} from '@microsoft/signalr';

const reconnectDelaysMs = [0, 2_000, 10_000];

export const signalRReconnectPolicy: IRetryPolicy = {
  nextRetryDelayInMilliseconds: context => reconnectDelaysMs[context.previousRetryCount] ?? 30_000
};

// Automatic reconnect bypasses lifecycle.start(), which handles initial-start auth.
// Renew the cookie here so each native reconnect attempt can negotiate a session.
export class ReconnectHttpClient extends DefaultHttpClient {
  constructor(
    private readonly isReconnecting: () => boolean,
    private readonly refreshSession: () => Promise<boolean>
  ) {
    super(NullLogger.instance);
  }

  override async send(request: HttpRequest) {
    try {
      return await super.send(request);
    } catch (error) {
      const isNegotiation = request.method === 'POST'
        && request.url !== undefined
        && new URL(request.url).pathname.endsWith('/negotiate');
      if (!(error instanceof HttpError) || error.statusCode !== 401
        || !isNegotiation || !this.isReconnecting()) {
        throw error;
      }

      // Session refresh is shared with ordinary API calls. Let it finish independently
      // so it cannot hold up native retries or stopping this connection.
      void this.refreshSession().catch(() => undefined);
      throw error;
    }
  }
}
