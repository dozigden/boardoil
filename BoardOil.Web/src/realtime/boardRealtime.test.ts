import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

type FakeConnection = {
  on: ReturnType<typeof vi.fn>;
  onreconnected: ReturnType<typeof vi.fn>;
  start: ReturnType<typeof vi.fn>;
  stop: ReturnType<typeof vi.fn>;
  invoke: ReturnType<typeof vi.fn>;
  eventHandlers: Record<string, (...args: unknown[]) => unknown>;
  reconnectHandler: (() => Promise<unknown> | unknown) | null;
};

let connection: FakeConnection;

vi.mock('@microsoft/signalr', () => {
  connection = {
    eventHandlers: {},
    reconnectHandler: null,
    on: vi.fn((event: string, handler: (...args: unknown[]) => unknown) => {
      connection.eventHandlers[event] = handler;
      return connection;
    }),
    onreconnected: vi.fn((handler: () => Promise<unknown> | unknown) => {
      connection.reconnectHandler = handler;
      return connection;
    }),
    start: vi.fn(async () => undefined),
    stop: vi.fn(async () => undefined),
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

  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

});
