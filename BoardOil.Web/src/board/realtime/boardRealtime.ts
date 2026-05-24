import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { boardHubUrl } from '../../shared/api/config';
import { attemptSessionRefresh } from '../../shared/api/http';
import type { Card, CardComment, Column } from '../../shared/types/boardTypes';
import type { SystemInfoMessageDto } from '../../shared/types/configurationTypes';

type RealtimeHandlers = {
  onColumnCreated: (column: Column) => Promise<unknown> | unknown;
  onColumnUpdated: (column: Column) => Promise<unknown> | unknown;
  onColumnDeleted: (columnId: number) => Promise<unknown> | unknown;
  onCardCreated: (card: Card) => Promise<unknown> | unknown;
  onCardUpdated: (card: Card) => Promise<unknown> | unknown;
  onCardDeleted: (cardId: number) => Promise<unknown> | unknown;
  onCardMoved: (card: Card) => Promise<unknown> | unknown;
  onCommentCreated: (comment: CardComment) => Promise<unknown> | unknown;
  onSystemInfoMessageUpdated: (systemInfoMessage: SystemInfoMessageDto | null) => Promise<unknown> | unknown;
  onResync: () => Promise<unknown> | unknown;
};

const realtimeDebugEnabled = resolveRealtimeDebugEnabled();
const signalRLogLevel = realtimeDebugEnabled ? LogLevel.Information : LogLevel.Warning;
const unauthorizedStartRetryDelaysMs = [0, 1_000, 3_000, 7_000];

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

export function createBoardRealtime(handlers: RealtimeHandlers) {
  let hubConnection: HubConnection | null = null;
  let subscribedBoardId: number | null = null;
  let startPromise: Promise<void> | null = null;
  let subscribePromise: Promise<void> | null = null;

  async function ensureConnectionStarted() {
    if (!hubConnection) {
      return;
    }

    if (hubConnection.state === HubConnectionState.Connected) {
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
        await hubConnection.start();
      } catch (error) {
        if (!isUnauthorizedNegotiationError(error)) {
          throw error;
        }

        await retryUnauthorizedStart(hubConnection, error);
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

      hubConnection.on('ColumnCreated', async (column: Column) => {
        logRealtime('Event: ColumnCreated', { columnId: column.id });
        await handlers.onColumnCreated(column);
      });
      hubConnection.on('ColumnUpdated', async (column: Column) => {
        logRealtime('Event: ColumnUpdated', { columnId: column.id });
        await handlers.onColumnUpdated(column);
      });
      hubConnection.on('ColumnDeleted', async (columnId: number) => {
        logRealtime('Event: ColumnDeleted', { columnId });
        await handlers.onColumnDeleted(columnId);
      });
      hubConnection.on('CardCreated', async (card: Card) => {
        logRealtime('Event: CardCreated', { cardId: card.id, boardColumnId: card.boardColumnId });
        await handlers.onCardCreated(card);
      });
      hubConnection.on('CardUpdated', async (card: Card) => {
        logRealtime('Event: CardUpdated', { cardId: card.id, boardColumnId: card.boardColumnId });
        await handlers.onCardUpdated(card);
      });
      hubConnection.on('CardDeleted', async (cardId: number) => {
        logRealtime('Event: CardDeleted', { cardId });
        await handlers.onCardDeleted(cardId);
      });
      hubConnection.on('CardMoved', async (card: Card) => {
        logRealtime('Event: CardMoved', { cardId: card.id, boardColumnId: card.boardColumnId });
        await handlers.onCardMoved(card);
      });
      hubConnection.on('CommentCreated', async (comment: CardComment) => {
        logRealtime('Event: CommentCreated', { commentId: comment.id, cardId: comment.cardId });
        await handlers.onCommentCreated(comment);
      });
      hubConnection.on('ResyncRequested', async () => {
        logRealtime('Event: ResyncRequested');
        await handlers.onResync();
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
      });

      hubConnection.onreconnected(async () => {
        logRealtime('Connection reconnected.', { subscribedBoardId });

        if (subscribedBoardId !== null) {
          await hubConnection?.invoke('SubscribeBoard', subscribedBoardId);
          logRealtime('Re-subscribed after reconnect.', { boardId: subscribedBoardId });
        }

        await handlers.onResync();
        logRealtime('Resync requested after reconnect.');
      });

      hubConnection.onclose(error => {
        logRealtime('Connection closed.', {
          subscribedBoardId,
          error: error instanceof Error ? error.message : String(error)
        });
      });
    }

    await ensureConnectionStarted();
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
    if (hubConnection) {
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

async function retryUnauthorizedStart(connection: HubConnection, initialError: unknown) {
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
      return;
    } catch (retryError) {
      if (!isUnauthorizedNegotiationError(retryError)) {
        throw retryError;
      }

      latestUnauthorizedError = retryError;
      logRealtime('Realtime retry still unauthorized.');
    }
  }

  throw latestUnauthorizedError;
}

async function wait(delayMs: number) {
  await new Promise(resolve => {
    setTimeout(resolve, delayMs);
  });
}
