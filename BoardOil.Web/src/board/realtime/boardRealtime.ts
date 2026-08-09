import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { boardHubUrl } from '../../shared/api/config';
import { attemptSessionRefresh, notifyUnauthorized } from '../../shared/api/http';
import {
  clientErrorReporter,
  type ClientErrorReporter
} from '../../shared/errors/clientErrorReporter';
import type { Card, CardComment, Column } from '../../shared/types/boardTypes';
import type { SystemInfoMessageDto } from '../../shared/types/configurationTypes';

type RealtimeHandlers = {
  onColumnCreated: (boardId: number, column: Column) => Promise<unknown> | unknown;
  onColumnUpdated: (boardId: number, column: Column) => Promise<unknown> | unknown;
  onColumnDeleted: (boardId: number, columnId: number) => Promise<unknown> | unknown;
  onCardCreated: (boardId: number, card: Card) => Promise<unknown> | unknown;
  onCardUpdated: (boardId: number, card: Card) => Promise<unknown> | unknown;
  onCardDeleted: (boardId: number, cardId: number) => Promise<unknown> | unknown;
  onCardMoved: (boardId: number, card: Card) => Promise<unknown> | unknown;
  onCommentCreated: (boardId: number, comment: CardComment) => Promise<unknown> | unknown;
  onSystemInfoMessageUpdated: (systemInfoMessage: SystemInfoMessageDto | null) => Promise<unknown> | unknown;
  onResync: (boardId: number) => Promise<unknown> | unknown;
  onConnectionWarning?: (message: string) => Promise<unknown> | unknown;
  onConnectionRecovered?: () => Promise<unknown> | unknown;
};

const realtimeDebugEnabled = resolveRealtimeDebugEnabled();
const signalRLogLevel = realtimeDebugEnabled ? LogLevel.Information : LogLevel.Warning;
const unauthorizedStartRetryDelaysMs = [0, 1_000, 3_000, 7_000];
const realtimeDisconnectedMessage = 'Realtime updates are unavailable. Data may be stale until reconnect.';

function logRealtime(message: string, details?: unknown) {
  if (!realtimeDebugEnabled) {
    return;
  }

  if (details === undefined) {
    console.log(`[board-realtime] ${message}`);
    return;
  }

  console.log(`[board-realtime] ${message}`, details);
}

function resolveRealtimeDebugEnabled() {
  if (import.meta.env.DEV) {
    return true;
  }

  try {
    const localStorageValue = globalThis.localStorage?.getItem('boardoil:realtime-debug');
    if (localStorageValue === '1' || localStorageValue === 'true') {
      return true;
    }
  } catch {
    // Ignore localStorage access errors and continue.
  }

  const search = typeof window !== 'undefined' ? window.location?.search ?? '' : '';
  return search.includes('realtimeDebug=1') || search.includes('realtimeDebug=true');
}

export function createBoardRealtime(
  handlers: RealtimeHandlers,
  errorReporter: ClientErrorReporter = clientErrorReporter
) {
  let hubConnection: HubConnection | null = null;
  let subscribedBoardId: number | null = null;
  let startPromise: Promise<void> | null = null;
  let subscribePromise: Promise<void> | null = null;
  let pageUnloading = false;
  let disconnecting = false;

  registerPageUnloadListeners(() => {
    pageUnloading = true;
  }, () => {
    pageUnloading = false;
  });

  function emitConnectionWarning() {
    if (pageUnloading) {
      logRealtime('Suppressing realtime warning during page unload.');
      return;
    }

    return handlers.onConnectionWarning?.(realtimeDisconnectedMessage);
  }

  function reportRealtimeDiagnostic(
    phase: string,
    error: unknown,
    boardId: number | null,
    connection: HubConnection
  ) {
    if (pageUnloading || disconnecting || isUnauthorizedNegotiationError(error)) {
      return;
    }

    void errorReporter.reportRealtimeDiagnostic(phase, error, {
      boardId,
      connectionState: connection.state
    });
  }

  async function ensureConnectionStarted(boardId: number) {
    const connection = hubConnection;
    if (!connection) {
      return;
    }

    if (connection.state === HubConnectionState.Connected) {
      return;
    }

    if (startPromise) {
      logRealtime('Waiting for existing connection start.');
      await startPromise;
      return;
    }

    logRealtime('Starting realtime connection.');
    startPromise = (async () => {
      try {
        await connection.start();
        await handlers.onConnectionRecovered?.();
      } catch (error) {
        if (!isUnauthorizedNegotiationError(error)) {
          reportRealtimeDiagnostic('realtime-start-failed', error, boardId, connection);
          await emitConnectionWarning();
          throw error;
        }

        try {
          await retryUnauthorizedStart(connection, error, handlers.onConnectionRecovered);
        } catch (retryError) {
          if (!isUnauthorizedNegotiationError(retryError)) {
            reportRealtimeDiagnostic('realtime-start-failed', retryError, boardId, connection);
          }

          throw retryError;
        }
      }
    })().finally(() => {
      startPromise = null;
    });

    await startPromise;
    logRealtime('Realtime connection started.');
  }

  async function connect(boardId: number) {
    logRealtime('Connect requested.', { boardId });

    if (!hubConnection) {
      logRealtime('Creating hub connection.');
      hubConnection = new HubConnectionBuilder()
        .withUrl(boardHubUrl)
        .withAutomaticReconnect()
        .configureLogging(signalRLogLevel)
        .build();
      const connectionForHandlers = hubConnection;

      hubConnection.on('ColumnCreated', async (column: Column, boardId: number) => {
        logRealtime('Event: ColumnCreated', { boardId, columnId: column.id });
        await handlers.onColumnCreated(boardId, column);
      });
      hubConnection.on('ColumnUpdated', async (column: Column, boardId: number) => {
        logRealtime('Event: ColumnUpdated', { boardId, columnId: column.id });
        await handlers.onColumnUpdated(boardId, column);
      });
      hubConnection.on('ColumnDeleted', async (columnId: number, boardId: number) => {
        logRealtime('Event: ColumnDeleted', { boardId, columnId });
        await handlers.onColumnDeleted(boardId, columnId);
      });
      hubConnection.on('CardCreated', async (card: Card, boardId: number) => {
        logRealtime('Event: CardCreated', { boardId, cardId: card.id, boardColumnId: card.boardColumnId });
        await handlers.onCardCreated(boardId, card);
      });
      hubConnection.on('CardUpdated', async (card: Card, boardId: number) => {
        logRealtime('Event: CardUpdated', { boardId, cardId: card.id, boardColumnId: card.boardColumnId });
        await handlers.onCardUpdated(boardId, card);
      });
      hubConnection.on('CardDeleted', async (cardId: number, boardId: number) => {
        logRealtime('Event: CardDeleted', { boardId, cardId });
        await handlers.onCardDeleted(boardId, cardId);
      });
      hubConnection.on('CardMoved', async (card: Card, boardId: number) => {
        logRealtime('Event: CardMoved', { boardId, cardId: card.id, boardColumnId: card.boardColumnId });
        await handlers.onCardMoved(boardId, card);
      });
      hubConnection.on('CommentCreated', async (comment: CardComment, boardId: number) => {
        logRealtime('Event: CommentCreated', { boardId, commentId: comment.id, cardId: comment.cardId });
        await handlers.onCommentCreated(boardId, comment);
      });
      hubConnection.on('ResyncRequested', async (boardId: number) => {
        logRealtime('Event: ResyncRequested', { boardId });
        await handlers.onResync(boardId);
      });
      hubConnection.on('SystemInfoMessageUpdated', async (systemInfoMessage: SystemInfoMessageDto | null) => {
        logRealtime('Event: SystemInfoMessageUpdated');
        await handlers.onSystemInfoMessageUpdated(systemInfoMessage);
      });

      hubConnection.onreconnecting(error => {
        logRealtime('Connection reconnecting.', {
          subscribedBoardId,
          error: error instanceof Error ? error.message : String(error)
        });

        if (error) {
          reportRealtimeDiagnostic(
            'realtime-reconnecting',
            error,
            subscribedBoardId,
            connectionForHandlers);
        }
        void emitConnectionWarning();
      });

      hubConnection.onreconnected(async () => {
        logRealtime('Connection reconnected.', { subscribedBoardId });

        if (subscribedBoardId !== null) {
          await hubConnection?.invoke('SubscribeBoard', subscribedBoardId);
          logRealtime('Re-subscribed after reconnect.', { boardId: subscribedBoardId });
          await handlers.onResync(subscribedBoardId);
        }

        await handlers.onConnectionRecovered?.();
        logRealtime('Resync requested after reconnect.');
      });

      hubConnection.onclose(error => {
        logRealtime('Connection closed.', {
          subscribedBoardId,
          error: error instanceof Error ? error.message : String(error)
        });

        if (error) {
          reportRealtimeDiagnostic(
            'realtime-closed',
            error,
            subscribedBoardId,
            connectionForHandlers);
          void emitConnectionWarning();
        }
      });
    }

    await ensureConnectionStarted(boardId);
    await subscribeBoard(boardId);
  }

  async function subscribeBoard(boardId: number) {
    if (!hubConnection) {
      return;
    }

    if (subscribePromise) {
      logRealtime('Waiting for existing board subscription update.', { boardId });
      await subscribePromise;

      if (subscribedBoardId === boardId) {
        return;
      }
    }

    subscribePromise = (async () => {
      if (subscribedBoardId !== null && subscribedBoardId !== boardId) {
        logRealtime('Unsubscribing previous board.', { boardId: subscribedBoardId });
        await hubConnection.invoke('UnsubscribeBoard', subscribedBoardId);
      }

      if (subscribedBoardId !== boardId) {
        logRealtime('Subscribing board.', { boardId });
        await hubConnection.invoke('SubscribeBoard', boardId);
        subscribedBoardId = boardId;
      }
    })().finally(() => {
      subscribePromise = null;
    });

    await subscribePromise;
  }

  async function disconnect() {
    if (!hubConnection) {
      return;
    }

    disconnecting = true;
    try {
      logRealtime('Disconnect requested.', { subscribedBoardId });
      const connection = hubConnection;
      const boardId = subscribedBoardId;
      const pendingStart = startPromise;
      const pendingSubscribe = subscribePromise;

      if (pendingStart) {
        try {
          logRealtime('Waiting for pending start before disconnect.');
          await pendingStart;
        } catch {
          // If startup failed, continue teardown.
        }
      }

      if (pendingSubscribe) {
        try {
          logRealtime('Waiting for pending subscribe update before disconnect.');
          await pendingSubscribe;
        } catch {
          // If subscription failed, continue teardown.
        }
      }

      try {
        if (boardId !== null) {
          await connection.invoke('UnsubscribeBoard', boardId);
        }
      } catch {
        // Best-effort cleanup; continue stopping the connection.
      } finally {
        await connection.stop();
        logRealtime('Connection stopped.');
        if (hubConnection === connection) {
          hubConnection = null;
        }
        subscribedBoardId = null;
      }
    } finally {
      disconnecting = false;
    }
  }

  return {
    connect,
    disconnect,
  };
}

function isUnauthorizedNegotiationError(error: unknown) {
  if (!(error instanceof Error)) {
    return false;
  }

  const message = error.message.toLowerCase();
  return message.includes("status code '401'")
    || message.includes('status code 401')
    || message.includes('unauthorized');
}

async function retryUnauthorizedStart(
  connection: HubConnection,
  initialError: unknown,
  onConnectionRecovered?: () => Promise<unknown> | unknown
) {
  let latestUnauthorizedError = initialError;

  for (const delayMs of unauthorizedStartRetryDelaysMs) {
    if (delayMs > 0) {
      logRealtime('Waiting before unauthorized realtime retry.', { delayMs });
      await wait(delayMs);
    }

    logRealtime('Attempting session refresh before realtime retry.');
    const refreshed = await attemptSessionRefresh();
    if (!refreshed) {
      logRealtime('Session refresh failed before realtime retry.');
      continue;
    }

    try {
      logRealtime('Retrying realtime start after session refresh.');
      await connection.start();
      await onConnectionRecovered?.();
      return;
    } catch (retryError) {
      if (!isUnauthorizedNegotiationError(retryError)) {
        throw retryError;
      }

      latestUnauthorizedError = retryError;
      logRealtime('Realtime retry still unauthorized.');
    }
  }

  notifyUnauthorized();
  throw latestUnauthorizedError;
}

async function wait(delayMs: number) {
  await new Promise(resolve => {
    setTimeout(resolve, delayMs);
  });
}

function registerPageUnloadListeners(onUnload: () => void, onRestore: () => void) {
  const windowRef = globalThis.window;
  if (!windowRef || typeof windowRef.addEventListener !== 'function') {
    return;
  }

  windowRef.addEventListener('beforeunload', onUnload);
  windowRef.addEventListener('pagehide', onUnload);
  windowRef.addEventListener('pageshow', onRestore);
}
