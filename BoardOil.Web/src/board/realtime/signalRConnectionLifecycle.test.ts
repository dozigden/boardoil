import { HubConnectionState, type HubConnection } from '@microsoft/signalr';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createSignalRConnectionLifecycle } from './signalRConnectionLifecycle';

type FakeConnection = {
  state: HubConnectionState;
  start: ReturnType<typeof vi.fn>;
  stop: ReturnType<typeof vi.fn>;
  onreconnecting: ReturnType<typeof vi.fn>;
  onreconnected: ReturnType<typeof vi.fn>;
  onclose: ReturnType<typeof vi.fn>;
  reconnectingHandler: ((error?: Error) => Promise<unknown> | unknown) | null;
  reconnectedHandler: (() => Promise<unknown> | unknown) | null;
  closeHandler: ((error?: Error) => Promise<unknown> | unknown) | null;
};

describe('signalRConnectionLifecycle', () => {
  beforeEach(() => {
    vi.useRealTimers();
  });

  it('shares an in-flight connection start', async () => {
    const harness = createHarness();
    let finishStart!: () => void;
    harness.connection.start.mockImplementation(
      () => new Promise<void>(resolve => {
        finishStart = () => {
          harness.connection.state = HubConnectionState.Connected;
          resolve();
        };
      })
    );

    const firstStart = harness.lifecycle.start();
    const secondStart = harness.lifecycle.start();

    expect(harness.connection.start).toHaveBeenCalledTimes(1);
    finishStart();
    await Promise.all([firstStart, secondStart]);
  });

  it('refreshes authentication and retries an unauthorized start', async () => {
    const harness = createHarness();
    harness.connection.start
      .mockRejectedValueOnce(new Error("Failed negotiation: Status code '401'"))
      .mockImplementationOnce(async () => {
        harness.connection.state = HubConnectionState.Connected;
      });
    harness.attemptAuthenticationRefresh.mockResolvedValueOnce(true);

    await harness.lifecycle.start();

    expect(harness.attemptAuthenticationRefresh).toHaveBeenCalledTimes(1);
    expect(harness.connection.start).toHaveBeenCalledTimes(2);
    expect(harness.reportDiagnostic).not.toHaveBeenCalled();
  });

  it('does not report recovery until reconnect state restoration completes', async () => {
    const harness = createHarness();
    let finishRestore!: () => void;
    harness.restoreAfterReconnect.mockImplementation(
      () => new Promise<void>(resolve => {
        finishRestore = resolve;
      })
    );

    await harness.lifecycle.start();
    harness.connection.state = HubConnectionState.Reconnecting;
    harness.connection.reconnectingHandler?.(new Error('network'));
    harness.connection.state = HubConnectionState.Connected;
    const reconnect = harness.connection.reconnectedHandler?.();

    expect(harness.onUnavailable).toHaveBeenCalledTimes(1);
    expect(harness.onRecovered).not.toHaveBeenCalled();

    finishRestore();
    await reconnect;

    expect(harness.onRecovered).toHaveBeenCalledTimes(1);
    expect(harness.restoreAfterReconnect.mock.invocationCallOrder[0]).toBeLessThan(
      harness.onRecovered.mock.invocationCallOrder[0]!);
  });

  it('keeps new starts waiting for reconnect state restoration', async () => {
    const harness = createHarness();
    let finishRestore!: () => void;
    harness.restoreAfterReconnect.mockImplementation(
      () => new Promise<void>(resolve => {
        finishRestore = resolve;
      })
    );

    await harness.lifecycle.start();
    harness.connection.state = HubConnectionState.Reconnecting;
    harness.connection.reconnectingHandler?.(new Error('network'));
    const waitingStart = harness.lifecycle.start();

    harness.connection.state = HubConnectionState.Connected;
    const reconnect = harness.connection.reconnectedHandler?.();
    let startFinished = false;
    void waitingStart.then(() => {
      startFinished = true;
    });
    await Promise.resolve();

    expect(startFinished).toBe(false);
    expect(harness.connection.start).toHaveBeenCalledTimes(1);

    finishRestore();
    await Promise.all([reconnect, waitingStart]);
    expect(startFinished).toBe(true);
  });

  it('does not report automatic reconnect recovery when stopped during restoration', async () => {
    const harness = createHarness();
    let finishRestore!: () => void;
    harness.restoreAfterReconnect.mockImplementation(
      () => new Promise<void>(resolve => {
        finishRestore = resolve;
      })
    );

    await harness.lifecycle.start();
    harness.connection.state = HubConnectionState.Reconnecting;
    harness.connection.reconnectingHandler?.(new Error('network'));
    harness.connection.state = HubConnectionState.Connected;
    const reconnect = harness.connection.reconnectedHandler?.();
    await vi.waitFor(() => {
      expect(harness.restoreAfterReconnect).toHaveBeenCalledTimes(1);
    });

    await harness.lifecycle.stop();
    finishRestore();
    await reconnect;

    expect(harness.onRecovered).not.toHaveBeenCalled();
  });

  it('restarts, restores state, and then reports recovery after a terminal close', async () => {
    const harness = createHarness();

    await harness.lifecycle.start();
    harness.connection.state = HubConnectionState.Disconnected;
    await harness.connection.closeHandler?.();

    expect(harness.connection.start).toHaveBeenCalledTimes(2);
    expect(harness.restoreAfterReconnect).toHaveBeenCalledTimes(1);
    expect(harness.onRecovered).toHaveBeenCalledTimes(1);
    expect(harness.restoreAfterReconnect.mock.invocationCallOrder[0]).toBeLessThan(
      harness.onRecovered.mock.invocationCallOrder[0]!);
  });

  it('does not report terminal-close recovery when stopped during restoration', async () => {
    const harness = createHarness();
    let finishRestore!: () => void;
    harness.restoreAfterReconnect.mockImplementation(
      () => new Promise<void>(resolve => {
        finishRestore = resolve;
      })
    );

    await harness.lifecycle.start();
    harness.connection.state = HubConnectionState.Disconnected;
    const recovery = harness.connection.closeHandler?.();
    await vi.waitFor(() => {
      expect(harness.restoreAfterReconnect).toHaveBeenCalledTimes(1);
    });

    await harness.lifecycle.stop();
    finishRestore();
    await recovery;

    expect(harness.onRecovered).not.toHaveBeenCalled();
  });

  it('caps terminal-close recovery at three complete attempts', async () => {
    vi.useFakeTimers();
    const harness = createHarness();

    await harness.lifecycle.start();
    harness.connection.start.mockRejectedValue(new Error('network unavailable'));
    harness.connection.state = HubConnectionState.Disconnected;
    const recovery = harness.connection.closeHandler?.();

    await vi.runAllTimersAsync();
    await recovery;

    expect(harness.connection.start).toHaveBeenCalledTimes(4);
    expect(harness.restoreAfterReconnect).not.toHaveBeenCalled();
    const recoveryDiagnostics = harness.reportDiagnostic.mock.calls
      .filter(([phase]) => phase === 'realtime-recovery-failed');
    expect(recoveryDiagnostics).toHaveLength(3);
    expect(harness.onRecoveryExhausted).toHaveBeenCalledTimes(1);
  });

  it('suppresses close recovery and diagnostics during intentional stop', async () => {
    const harness = createHarness();
    harness.connection.stop.mockImplementation(async () => {
      await harness.connection.closeHandler?.(new Error('intentional stop'));
      harness.connection.state = HubConnectionState.Disconnected;
    });

    await harness.lifecycle.start();
    await harness.lifecycle.stop();

    expect(harness.connection.start).toHaveBeenCalledTimes(1);
    expect(harness.onUnavailable).not.toHaveBeenCalled();
    expect(harness.reportDiagnostic).not.toHaveBeenCalled();

    await harness.connection.closeHandler?.(new Error('delayed close'));
    await harness.connection.reconnectedHandler?.();
    harness.connection.reconnectingHandler?.(new Error('delayed reconnect'));

    expect(harness.connection.start).toHaveBeenCalledTimes(1);
    expect(harness.restoreAfterReconnect).not.toHaveBeenCalled();
    expect(harness.onRecovered).not.toHaveBeenCalled();
    expect(harness.onUnavailable).not.toHaveBeenCalled();
    expect(harness.reportDiagnostic).not.toHaveBeenCalled();
  });

  afterEach(() => {
    vi.useRealTimers();
  });
});

function createHarness() {
  const connection: FakeConnection = {
    state: HubConnectionState.Disconnected,
    reconnectingHandler: null,
    reconnectedHandler: null,
    closeHandler: null,
    start: vi.fn(async () => {
      connection.state = HubConnectionState.Connected;
    }),
    stop: vi.fn(async () => {
      connection.state = HubConnectionState.Disconnected;
    }),
    onreconnecting: vi.fn(handler => {
      connection.reconnectingHandler = handler;
    }),
    onreconnected: vi.fn(handler => {
      connection.reconnectedHandler = handler;
    }),
    onclose: vi.fn(handler => {
      connection.closeHandler = handler;
    })
  };
  const attemptAuthenticationRefresh = vi.fn(async () => false);
  const notifyAuthenticationFailure = vi.fn();
  const restoreAfterReconnect = vi.fn(async (): Promise<void> => undefined);
  const onUnavailable = vi.fn(async (): Promise<void> => undefined);
  const onRecoveryExhausted = vi.fn(async (): Promise<void> => undefined);
  const onRecovered = vi.fn(async (): Promise<void> => undefined);
  const reportDiagnostic = vi.fn();
  const lifecycle = createSignalRConnectionLifecycle({
    connection: connection as unknown as HubConnection,
    attemptAuthenticationRefresh,
    notifyAuthenticationFailure,
    restoreAfterReconnect,
    onUnavailable,
    onRecoveryExhausted,
    onRecovered,
    reportDiagnostic,
    isSuppressed: () => false,
    log: vi.fn()
  });

  return {
    connection,
    lifecycle,
    attemptAuthenticationRefresh,
    notifyAuthenticationFailure,
    restoreAfterReconnect,
    onUnavailable,
    onRecoveryExhausted,
    onRecovered,
    reportDiagnostic
  };
}
