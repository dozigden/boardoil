import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

type FakeConnection = {
  on: ReturnType<typeof vi.fn>;
  onreconnecting: ReturnType<typeof vi.fn>;
  onreconnected: ReturnType<typeof vi.fn>;
  onclose: ReturnType<typeof vi.fn>;
  start: ReturnType<typeof vi.fn>;
  stop: ReturnType<typeof vi.fn>;
  invoke: ReturnType<typeof vi.fn>;
  state: string;
  eventHandlers: Record<string, (...args: unknown[]) => unknown>;
  reconnectHandler: (() => Promise<unknown> | unknown) | null;
};

let connection: FakeConnection;
const attemptSessionRefresh = vi.fn(async () => false);

vi.mock('../../shared/api/http', () => ({
  attemptSessionRefresh
}));

vi.mock('@microsoft/signalr', () => {
  connection = {
    eventHandlers: {},
    reconnectHandler: null,
    state: 'Disconnected',
    on: vi.fn((event: string, handler: (...args: unknown[]) => unknown) => {
      connection.eventHandlers[event] = handler;
      return connection;
    }),
    onreconnecting: vi.fn(() => connection),
    onreconnected: vi.fn((handler: () => Promise<unknown> | unknown) => {
      connection.reconnectHandler = handler;
      return connection;
    }),
    onclose: vi.fn(() => connection),
    start: vi.fn(async () => {
      connection.state = 'Connected';
    }),
    stop: vi.fn(async () => {
      connection.state = 'Disconnected';
    }),
    invoke: vi.fn(async () => undefined)
  };

  class HubConnectionBuilder {
    withUrl() {
      return this;
    }

    withAutomaticReconnect() {
      return this;
    }

    configureLogging() {
      return this;
    }

    build() {
      return connection;
    }
  }

  return {
    HubConnectionBuilder,
    HubConnectionState: {
      Connected: 'Connected'
    },
    LogLevel: {
      Warning: 'Warning',
      Information: 'Information',
      None: 'None'
    }
  };
});

describe('boardRealtime', () => {
  beforeEach(() => {
    vi.useRealTimers();
    vi.clearAllMocks();
    vi.resetModules();
    attemptSessionRefresh.mockReset();
    attemptSessionRefresh.mockResolvedValue(false);
    vi.stubGlobal('window', {
      location: {
        origin: 'http://localhost:5173'
      }
    });
  });

  it('resyncs on reconnect callback', async () => {
    const onResync = vi.fn(async () => undefined);
    const { createBoardRealtime } = await import('./boardRealtime');
    const realtime = createBoardRealtime({
      onColumnCreated: vi.fn(),
      onColumnUpdated: vi.fn(),
      onColumnDeleted: vi.fn(),
      onCardCreated: vi.fn(),
      onCardUpdated: vi.fn(),
      onCardDeleted: vi.fn(),
      onCardMoved: vi.fn(),
      onCommentCreated: vi.fn(),
      onSystemInfoMessageUpdated: vi.fn(),
      onResync
    });

    await realtime.connect(42);
    await connection.reconnectHandler?.();

    expect(connection.invoke).toHaveBeenCalledWith('SubscribeBoard', 42);
    expect(onResync).toHaveBeenCalledTimes(1);
  });

  it('resyncs when explicit resync event is received', async () => {
    const onResync = vi.fn(async () => undefined);
    const { createBoardRealtime } = await import('./boardRealtime');
    const realtime = createBoardRealtime({
      onColumnCreated: vi.fn(),
      onColumnUpdated: vi.fn(),
      onColumnDeleted: vi.fn(),
      onCardCreated: vi.fn(),
      onCardUpdated: vi.fn(),
      onCardDeleted: vi.fn(),
      onCardMoved: vi.fn(),
      onCommentCreated: vi.fn(),
      onSystemInfoMessageUpdated: vi.fn(),
      onResync
    });

    await realtime.connect(42);
    await connection.eventHandlers.ResyncRequested?.();

    expect(onResync).toHaveBeenCalledTimes(1);
  });

  it('forwards comment created events to handler', async () => {
    const onCommentCreated = vi.fn(async () => undefined);
    const { createBoardRealtime } = await import('./boardRealtime');
    const realtime = createBoardRealtime({
      onColumnCreated: vi.fn(),
      onColumnUpdated: vi.fn(),
      onColumnDeleted: vi.fn(),
      onCardCreated: vi.fn(),
      onCardUpdated: vi.fn(),
      onCardDeleted: vi.fn(),
      onCardMoved: vi.fn(),
      onCommentCreated,
      onSystemInfoMessageUpdated: vi.fn(),
      onResync: vi.fn()
    });

    const comment = {
      id: 123,
      cardId: 55,
      authorUserId: 4,
      text: 'hello',
      createdAtUtc: '2026-05-02T07:00:00.0000000Z'
    };

    await realtime.connect(42);
    await connection.eventHandlers.CommentCreated?.(comment);

    expect(onCommentCreated).toHaveBeenCalledTimes(1);
    expect(onCommentCreated).toHaveBeenCalledWith(comment);
  });

  it('forwards system info message update events to handler', async () => {
    const onSystemInfoMessageUpdated = vi.fn(async () => undefined);
    const { createBoardRealtime } = await import('./boardRealtime');
    const realtime = createBoardRealtime({
      onColumnCreated: vi.fn(),
      onColumnUpdated: vi.fn(),
      onColumnDeleted: vi.fn(),
      onCardCreated: vi.fn(),
      onCardUpdated: vi.fn(),
      onCardDeleted: vi.fn(),
      onCardMoved: vi.fn(),
      onCommentCreated: vi.fn(),
      onSystemInfoMessageUpdated,
      onResync: vi.fn()
    });

    const payload = {
      enabled: true,
      emoji: '⚠️',
      title: 'Maintenance',
      description: 'Service update incoming.',
      styleName: 'presets',
      stylePropertiesJson: '{"presetIndex":4}'
    };

    await realtime.connect(42);
    await connection.eventHandlers.SystemInfoMessageUpdated?.(payload);

    expect(onSystemInfoMessageUpdated).toHaveBeenCalledTimes(1);
    expect(onSystemInfoMessageUpdated).toHaveBeenCalledWith(payload);
  });

  it('still stops connection when unsubscribe fails during disconnect', async () => {
    const { createBoardRealtime } = await import('./boardRealtime');
    const realtime = createBoardRealtime({
      onColumnCreated: vi.fn(),
      onColumnUpdated: vi.fn(),
      onColumnDeleted: vi.fn(),
      onCardCreated: vi.fn(),
      onCardUpdated: vi.fn(),
      onCardDeleted: vi.fn(),
      onCardMoved: vi.fn(),
      onCommentCreated: vi.fn(),
      onSystemInfoMessageUpdated: vi.fn(),
      onResync: vi.fn()
    });
    connection.invoke.mockImplementation(async (method: string) => {
      if (method === 'UnsubscribeBoard') {
        throw new Error('unsubscribe failed');
      }
    });

    await realtime.connect(7);
    await realtime.disconnect();

    expect(connection.stop).toHaveBeenCalledTimes(1);
  });

  it('waits for in-flight start before subscribing during concurrent connect calls', async () => {
    let resolveStart!: () => void;
    connection.state = 'Disconnected';
    connection.start.mockImplementation(
      () => new Promise<void>(resolve => {
        resolveStart = () => {
          connection.state = 'Connected';
          resolve();
        };
      })
    );
    connection.invoke.mockImplementation(async (method: string) => {
      if (method === 'SubscribeBoard' && connection.state !== 'Connected') {
        throw new Error('subscribe called before connected');
      }
    });

    const { createBoardRealtime } = await import('./boardRealtime');
    const realtime = createBoardRealtime({
      onColumnCreated: vi.fn(),
      onColumnUpdated: vi.fn(),
      onColumnDeleted: vi.fn(),
      onCardCreated: vi.fn(),
      onCardUpdated: vi.fn(),
      onCardDeleted: vi.fn(),
      onCardMoved: vi.fn(),
      onCommentCreated: vi.fn(),
      onSystemInfoMessageUpdated: vi.fn(),
      onResync: vi.fn()
    });

    const firstConnect = realtime.connect(42);
    const secondConnect = realtime.connect(42);

    expect(connection.invoke).not.toHaveBeenCalledWith('SubscribeBoard', 42);

    resolveStart();
    await Promise.all([firstConnect, secondConnect]);

    expect(connection.start).toHaveBeenCalledTimes(1);
    expect(connection.invoke).toHaveBeenCalledTimes(1);
    expect(connection.invoke).toHaveBeenCalledWith('SubscribeBoard', 42);
  });

  it('refreshes session and retries connect once when negotiate returns 401', async () => {
    connection.state = 'Disconnected';
    connection.start
      .mockRejectedValueOnce(new Error("Failed to complete negotiation with the server: Status code '401'"))
      .mockImplementationOnce(async () => {
        connection.state = 'Connected';
      });
    attemptSessionRefresh.mockResolvedValueOnce(true);

    const { createBoardRealtime } = await import('./boardRealtime');
    const realtime = createBoardRealtime({
      onColumnCreated: vi.fn(),
      onColumnUpdated: vi.fn(),
      onColumnDeleted: vi.fn(),
      onCardCreated: vi.fn(),
      onCardUpdated: vi.fn(),
      onCardDeleted: vi.fn(),
      onCardMoved: vi.fn(),
      onCommentCreated: vi.fn(),
      onSystemInfoMessageUpdated: vi.fn(),
      onResync: vi.fn()
    });

    await realtime.connect(42);

    expect(attemptSessionRefresh).toHaveBeenCalledTimes(1);
    expect(connection.start).toHaveBeenCalledTimes(2);
    expect(connection.invoke).toHaveBeenCalledWith('SubscribeBoard', 42);
  });

  it('retries unauthorized negotiate over time and eventually connects', async () => {
    vi.useFakeTimers();
    connection.state = 'Disconnected';
    connection.start
      .mockRejectedValueOnce(new Error("Failed to complete negotiation with the server: Status code '401'"))
      .mockRejectedValueOnce(new Error("Failed to complete negotiation with the server: Status code '401'"))
      .mockImplementationOnce(async () => {
        connection.state = 'Connected';
      });
    attemptSessionRefresh.mockResolvedValue(true);

    const { createBoardRealtime } = await import('./boardRealtime');
    const realtime = createBoardRealtime({
      onColumnCreated: vi.fn(),
      onColumnUpdated: vi.fn(),
      onColumnDeleted: vi.fn(),
      onCardCreated: vi.fn(),
      onCardUpdated: vi.fn(),
      onCardDeleted: vi.fn(),
      onCardMoved: vi.fn(),
      onCommentCreated: vi.fn(),
      onSystemInfoMessageUpdated: vi.fn(),
      onResync: vi.fn()
    });

    const connectPromise = realtime.connect(42);
    await vi.runAllTimersAsync();
    await connectPromise;

    expect(attemptSessionRefresh).toHaveBeenCalledTimes(2);
    expect(connection.start).toHaveBeenCalledTimes(3);
    expect(connection.invoke).toHaveBeenCalledWith('SubscribeBoard', 42);
  });

  it('fails after timed unauthorized retries are exhausted', async () => {
    vi.useFakeTimers();
    connection.state = 'Disconnected';
    const unauthorizedError = new Error("Failed to complete negotiation with the server: Status code '401'");
    connection.start.mockRejectedValue(unauthorizedError);
    attemptSessionRefresh.mockResolvedValue(true);

    const { createBoardRealtime } = await import('./boardRealtime');
    const realtime = createBoardRealtime({
      onColumnCreated: vi.fn(),
      onColumnUpdated: vi.fn(),
      onColumnDeleted: vi.fn(),
      onCardCreated: vi.fn(),
      onCardUpdated: vi.fn(),
      onCardDeleted: vi.fn(),
      onCardMoved: vi.fn(),
      onCommentCreated: vi.fn(),
      onSystemInfoMessageUpdated: vi.fn(),
      onResync: vi.fn()
    });

    const connectPromise = realtime.connect(42);
    const rejectionExpectation = expect(connectPromise).rejects.toThrow(/status code '401'/i);
    await vi.runAllTimersAsync();
    await rejectionExpectation;
    expect(attemptSessionRefresh).toHaveBeenCalledTimes(4);
    expect(connection.start).toHaveBeenCalledTimes(5);
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

});
