import { beforeEach, describe, expect, it, vi } from 'vitest';

type TypingChangedHandler = (event: { cardId: number; isTyping: boolean; userLabel: string }) => void;

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

function makeLocalStorage(userLabel = 'Me') {
  let stored = userLabel;
  return {
    getItem: vi.fn((key: string) => (key === 'boardoil.userLabel' ? stored : null)),
    setItem: vi.fn((key: string, value: string) => {
      if (key === 'boardoil.userLabel') {
        stored = value;
      }
    })
  };
}

describe('boardRealtime', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.clearAllMocks();
    vi.resetModules();
    vi.stubGlobal('window', {
      location: {
        origin: 'http://localhost:5173'
      }
    });
    vi.stubGlobal('localStorage', makeLocalStorage('Me'));
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

    await realtime.connect();
    await connection.reconnectHandler?.();

    expect(onResync).toHaveBeenCalledTimes(1);
  });

  it('resets typing timeout during churn and sends one stop for latest timer', async () => {
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

    await realtime.connect();

    realtime.announceTyping(42);
    vi.advanceTimersByTime(1000);
    realtime.announceTyping(42);

    vi.advanceTimersByTime(1399);
    expect(connection.invoke).toHaveBeenCalledWith('TypingStarted', 42, 'Me');
    expect(connection.invoke).not.toHaveBeenCalledWith('TypingStopped', 42, 'Me');

    vi.advanceTimersByTime(1);
    await vi.runOnlyPendingTimersAsync();

    const stopCalls = connection.invoke.mock.calls.filter(
      args => args[0] === 'TypingStopped' && args[1] === 42 && args[2] === 'Me'
    );
    expect(stopCalls).toHaveLength(1);
  });

  it('typing summary ignores local user label and tracks remote user typing', async () => {
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

    await realtime.connect();

    const typingChanged = connection.eventHandlers['TypingChanged'] as TypingChangedHandler;

    typingChanged({ cardId: 10, isTyping: true, userLabel: 'Me' });
    expect(realtime.typingSummary.value(10)).toBe(false);

    typingChanged({ cardId: 10, isTyping: true, userLabel: 'Teammate' });
    expect(realtime.typingSummary.value(10)).toBe(true);

    typingChanged({ cardId: 10, isTyping: false, userLabel: 'Teammate' });
    expect(realtime.typingSummary.value(10)).toBe(false);
  });
});
