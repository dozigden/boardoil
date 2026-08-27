import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr';
import { boardHubUrl } from '../../shared/api/config';
import { attemptSessionRefresh, notifyUnauthorized } from '../../shared/api/http';
import {
  clientErrorReporter,
  type ClientErrorReporter
} from '../../shared/errors/clientErrorReporter';
import type { Card, CardComment, Column } from '../../shared/types/boardTypes';
import type { SystemInfoMessageDto } from '../../shared/types/configurationTypes';
import { readBrowserStorageItem } from '../../shared/utils/browserStorage';
import {
  createSignalRConnectionLifecycle,
  type SignalRConnectionLifecycle
} from './signalRConnectionLifecycle';

export type BoardRealtimeHandlers = {
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

export type BoardRealtime = {
  connect: (boardId: number) => Promise<void>;
  disconnect: () => Promise<void>;
};

type BoardRealtimeFactory = (handlers: BoardRealtimeHandlers) => BoardRealtime;

let boardRealtimeFactory: BoardRealtimeFactory = createSignalRBoardRealtime;

export function configureBoardRealtimeFactory(factory: BoardRealtimeFactory) {
  boardRealtimeFactory = factory;
}

const realtimeDebugEnabled = resolveRealtimeDebugEnabled();
const signalRLogLevel = realtimeDebugEnabled ? LogLevel.Information : LogLevel.Warning;
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

  const storageValue = readBrowserStorageItem('boardoil:realtime-debug');
  if (storageValue === '1' || storageValue === 'true') {
    return true;
  }

  const search = typeof window !== 'undefined' ? window.location?.search ?? '' : '';
  return search.includes('realtimeDebug=1') || search.includes('realtimeDebug=true');
}

export function createBoardRealtime(
  handlers: BoardRealtimeHandlers,
  errorReporter: ClientErrorReporter = clientErrorReporter
) {
  if (boardRealtimeFactory !== createSignalRBoardRealtime) {
    return boardRealtimeFactory(handlers);
  }

  return createSignalRBoardRealtime(handlers, errorReporter);
}

function createSignalRBoardRealtime(
  handlers: BoardRealtimeHandlers,
  errorReporter: ClientErrorReporter = clientErrorReporter
): BoardRealtime {
  let hubConnection: HubConnection | null = null;
  let connectionLifecycle: SignalRConnectionLifecycle | null = null;
  let activeBoardId: number | null = null;
  let requestedBoardId: number | null = null;
  let subscribedBoardId: number | null = null;
  let subscribePromise: Promise<void> | null = null;
  let pageUnloading = false;

  registerPageUnloadListeners(() => {
    pageUnloading = true;
  }, () => {
    pageUnloading = false;
  });

  async function connect(boardId: number) {
    logRealtime('Connect requested.', { boardId });
    requestedBoardId = boardId;
    const lifecycle = ensureConnection();

    await lifecycle.start();
    activeBoardId = boardId;
    await subscribeBoard(boardId);
    await handlers.onConnectionRecovered?.();
  }

  function ensureConnection() {
    if (hubConnection && connectionLifecycle) {
      return connectionLifecycle;
    }

    logRealtime('Creating hub connection.');
    const connection = new HubConnectionBuilder()
      .withUrl(boardHubUrl)
      .withAutomaticReconnect()
      .configureLogging(signalRLogLevel)
      .build();

    registerBoardEventHandlers(connection, handlers);
    hubConnection = connection;
    connectionLifecycle = createSignalRConnectionLifecycle({
      connection,
      attemptAuthenticationRefresh: attemptSessionRefresh,
      notifyAuthenticationFailure: notifyUnauthorized,
      restoreAfterReconnect: restoreActiveBoard,
      onUnavailable: () => handlers.onConnectionWarning?.(realtimeDisconnectedMessage),
      onRecovered: () => handlers.onConnectionRecovered?.(),
      reportDiagnostic: (phase, error, diagnosticConnection) => {
        void errorReporter.reportRealtimeDiagnostic(phase, error, {
          boardId: activeBoardId ?? requestedBoardId,
          connectionState: diagnosticConnection.state
        });
      },
      isSuppressed: () => pageUnloading,
      log: logRealtime
    });

    return connectionLifecycle;
  }

  async function restoreActiveBoard() {
    const boardId = activeBoardId;
    subscribedBoardId = null;
    if (boardId === null) {
      return;
    }

    await subscribeBoard(boardId);
    logRealtime('Re-subscribed after reconnect.', { boardId });
    await handlers.onResync(boardId);
  }

  async function subscribeBoard(boardId: number) {
    const connection = hubConnection;
    if (!connection) {
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
        await connection.invoke('UnsubscribeBoard', subscribedBoardId);
      }

      if (subscribedBoardId !== boardId) {
        logRealtime('Subscribing board.', { boardId });
        await connection.invoke('SubscribeBoard', boardId);
        subscribedBoardId = boardId;
      }
    })().finally(() => {
      subscribePromise = null;
    });

    await subscribePromise;
  }

  async function disconnect() {
    const connection = hubConnection;
    const lifecycle = connectionLifecycle;
    if (!connection || !lifecycle) {
      return;
    }

    logRealtime('Disconnect requested.', { subscribedBoardId });
    try {
      await lifecycle.stop(async () => {
        const pendingSubscribe = subscribePromise;
        if (pendingSubscribe) {
          try {
            logRealtime('Waiting for pending subscribe update before disconnect.');
            await pendingSubscribe;
          } catch {
            // If subscription failed, continue teardown.
          }
        }

        try {
          if (subscribedBoardId !== null) {
            await connection.invoke('UnsubscribeBoard', subscribedBoardId);
          }
        } catch {
          // Best-effort cleanup; continue stopping the connection.
        }
      });
    } finally {
      if (hubConnection === connection) {
        hubConnection = null;
        connectionLifecycle = null;
      }
      activeBoardId = null;
      requestedBoardId = null;
      subscribedBoardId = null;
    }
  }

  return {
    connect,
    disconnect
  };
}

function registerBoardEventHandlers(
  connection: HubConnection,
  handlers: BoardRealtimeHandlers
) {
  connection.on('ColumnCreated', async (column: Column, boardId: number) => {
    logRealtime('Event: ColumnCreated', { boardId, columnId: column.id });
    await handlers.onColumnCreated(boardId, column);
  });
  connection.on('ColumnUpdated', async (column: Column, boardId: number) => {
    logRealtime('Event: ColumnUpdated', { boardId, columnId: column.id });
    await handlers.onColumnUpdated(boardId, column);
  });
  connection.on('ColumnDeleted', async (columnId: number, boardId: number) => {
    logRealtime('Event: ColumnDeleted', { boardId, columnId });
    await handlers.onColumnDeleted(boardId, columnId);
  });
  connection.on('CardCreated', async (card: Card, boardId: number) => {
    logRealtime('Event: CardCreated', { boardId, cardId: card.id, boardColumnId: card.boardColumnId });
    await handlers.onCardCreated(boardId, card);
  });
  connection.on('CardUpdated', async (card: Card, boardId: number) => {
    logRealtime('Event: CardUpdated', { boardId, cardId: card.id, boardColumnId: card.boardColumnId });
    await handlers.onCardUpdated(boardId, card);
  });
  connection.on('CardDeleted', async (cardId: number, boardId: number) => {
    logRealtime('Event: CardDeleted', { boardId, cardId });
    await handlers.onCardDeleted(boardId, cardId);
  });
  connection.on('CardMoved', async (card: Card, boardId: number) => {
    logRealtime('Event: CardMoved', { boardId, cardId: card.id, boardColumnId: card.boardColumnId });
    await handlers.onCardMoved(boardId, card);
  });
  connection.on('CommentCreated', async (comment: CardComment, boardId: number) => {
    logRealtime('Event: CommentCreated', { boardId, commentId: comment.id, cardId: comment.cardId });
    await handlers.onCommentCreated(boardId, comment);
  });
  connection.on('ResyncRequested', async (boardId: number) => {
    logRealtime('Event: ResyncRequested', { boardId });
    await handlers.onResync(boardId);
  });
  connection.on('SystemInfoMessageUpdated', async (systemInfoMessage: SystemInfoMessageDto | null) => {
    logRealtime('Event: SystemInfoMessageUpdated');
    await handlers.onSystemInfoMessageUpdated(systemInfoMessage);
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
