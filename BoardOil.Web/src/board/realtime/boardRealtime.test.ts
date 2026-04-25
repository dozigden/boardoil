import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

type FakeConnection = {
  on: ReturnType<typeof vi.fn>;
  onreconnected: ReturnType<typeof vi.fn>;
  start: ReturnType<typeof vi.fn>;
  stop: ReturnType<typeof vi.fn>;
  invoke: ReturnType<typeof vi.fn>;
  state: string;
  eventHandlers: Record<string, (...args: unknown[]) => unknown>;
  reconnectHandler: (() => Promise<unknown> | unknown) | null;
};

let connection: FakeConnection;

vi.mock('@microsoft/signalr', () => {
  connection = {
    eventHandlers: {},
    reconnectHandler: null,
    state: 'Disconnected',
    on: vi.fn((event: string, handler: (...args: unknown[]) => unknown) => {
      connection.eventHandlers[event] = handler;
      return connection;
    }),
    onreconnected: vi.fn((handler: () => Promise<unknown> | unknown) => {
      connection.reconnectHandler = handler;
      return connection;
    }),
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
      Warning: 'Warning'
    }
  };
});

describe('boardRealtime', () => {
  beforeEach(() => {
    vi.useRealTimers();
    vi.clearAllMocks();
    vi.resetModules();
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
      onResync
    });

    await realtime.connect(42);
    await connection.eventHandlers.ResyncRequested?.();

    expect(onResync).toHaveBeenCalledTimes(1);
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

  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

});
