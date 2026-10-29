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
  let documentRef: EventTarget & { visibilityState: string };

  beforeEach(() => {
    vi.useFakeTimers();
    documentRef = Object.assign(new EventTarget(), { visibilityState: 'visible' });
    vi.stubGlobal('document', documentRef);
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

  it('reports a prolonged outage once and cancels brief outages on reconnection', async () => {
    const harness = createHarness();
    await harness.lifecycle.start();
    const error = new Error('network');
    harness.connection.reconnectingHandler?.(error);
    await vi.advanceTimersByTimeAsync(59_999);
    expect(harness.reportDiagnostic).not.toHaveBeenCalled();
    await harness.connection.reconnectedHandler?.();
    await vi.advanceTimersByTimeAsync(60_000);
    expect(harness.reportDiagnostic).not.toHaveBeenCalled();

    harness.connection.reconnectingHandler?.(error);
    await vi.advanceTimersByTimeAsync(60_000);
    expect(harness.reportDiagnostic).toHaveBeenCalledWith('realtime-outage', error, harness.connection);
    await harness.connection.closeHandler?.(error);
    await vi.advanceTimersByTimeAsync(120_000);
    expect(harness.reportDiagnostic).toHaveBeenCalledTimes(1);
    await harness.connection.reconnectedHandler?.();
    expect(harness.reportDiagnostic).toHaveBeenCalledTimes(1);
    expect(vi.getTimerCount()).toBe(0);
  });

  it.each([
    { waitBeforeRestart: 0, refreshAuthentication: false },
    { waitBeforeRestart: 60_000, refreshAuthentication: false },
    { waitBeforeRestart: 0, refreshAuthentication: true }
  ])('resets outage tracking after explicit restart: $waitBeforeRestart ms, auth refresh $refreshAuthentication', async ({ waitBeforeRestart, refreshAuthentication }) => {
    const harness = createHarness();
    await harness.lifecycle.start();
    harness.connection.state = HubConnectionState.Disconnected;
    await harness.connection.closeHandler?.(new Error('old outage'));
    await vi.advanceTimersByTimeAsync(waitBeforeRestart);
    harness.reportDiagnostic.mockClear();
    if (refreshAuthentication) {
      harness.connection.start.mockRejectedValueOnce(new Error("Failed negotiation: Status code '401'"));
      harness.attemptAuthenticationRefresh.mockResolvedValueOnce(true);
    }

    await harness.lifecycle.start();
    expect(harness.connection.state).toBe(HubConnectionState.Connected);
    expect(vi.getTimerCount()).toBe(0);
    await vi.advanceTimersByTimeAsync(60_000);
    expect(harness.reportDiagnostic).not.toHaveBeenCalled();

    harness.connection.state = HubConnectionState.Disconnected;
    const nextError = new Error('new outage');
    await harness.connection.closeHandler?.(nextError);
    await vi.advanceTimersByTimeAsync(60_000);
    expect(harness.reportDiagnostic).toHaveBeenCalledExactlyOnceWith('realtime-outage', nextError, harness.connection);
  });

  it('gives hidden tabs a fresh visible grace period', async () => {
    const harness = createHarness();
    await harness.lifecycle.start();
    harness.connection.reconnectingHandler?.(new Error('network'));
    await vi.advanceTimersByTimeAsync(50_000);
    documentRef.visibilityState = 'hidden';
    documentRef.dispatchEvent(new Event('visibilitychange'));
    await vi.advanceTimersByTimeAsync(3_600_000);
    expect(harness.reportDiagnostic).not.toHaveBeenCalled();
    expect(vi.getTimerCount()).toBe(0);
    documentRef.visibilityState = 'visible';
    documentRef.dispatchEvent(new Event('visibilitychange'));
    await vi.advanceTimersByTimeAsync(59_999);
    expect(harness.reportDiagnostic).not.toHaveBeenCalled();
    await vi.advanceTimersByTimeAsync(1);
    expect(harness.reportDiagnostic).toHaveBeenCalledTimes(1);
  });

  it('cancels the delayed report and removes its visibility listener on stop', async () => {
    const harness = createHarness();
    const removeListener = vi.spyOn(documentRef, 'removeEventListener');
    await harness.lifecycle.start();
    harness.connection.reconnectingHandler?.(new Error('network'));
    await vi.advanceTimersByTimeAsync(59_000);
    await harness.lifecycle.stop();
    documentRef.dispatchEvent(new Event('visibilitychange'));
    await vi.advanceTimersByTimeAsync(60_000);
    expect(harness.reportDiagnostic).not.toHaveBeenCalled();
    expect(vi.getTimerCount()).toBe(0);
    expect(removeListener).toHaveBeenCalledWith('visibilitychange', expect.any(Function));
  });

  it('reports restoration failure without leaking a rejected SignalR callback and rejects waiting starts', async () => {
    const harness = createHarness();
    await harness.lifecycle.start();
    harness.connection.state = HubConnectionState.Reconnecting;
    harness.connection.reconnectingHandler?.(new Error('network'));
    const waitingStart = harness.lifecycle.start();
    const rejectedStart = expect(waitingStart).rejects.toThrow('Subscription denied');
    harness.restoreAfterReconnect.mockRejectedValueOnce(new Error('Subscription denied'));
    harness.connection.state = HubConnectionState.Connected;

    await expect(harness.connection.reconnectedHandler?.()).resolves.toBeUndefined();
    await rejectedStart;
    expect(harness.onRecovered).not.toHaveBeenCalled();
    expect(harness.reportDiagnostic).toHaveBeenCalledWith(
      'realtime-restore-failed', expect.objectContaining({ message: 'Subscription denied' }), harness.connection);
    expect(vi.getTimerCount()).toBe(0);
  });

  it('reports a terminal close without restarting the connection', async () => {
    const harness = createHarness();
    await harness.lifecycle.start();
    harness.connection.state = HubConnectionState.Reconnecting;
    harness.connection.reconnectingHandler?.(new Error('network'));
    const waitingStart = harness.lifecycle.start();
    const rejection = expect(waitingStart).rejects.toThrow('Realtime connection closed.');

    harness.connection.state = HubConnectionState.Disconnected;
    await harness.connection.closeHandler?.();
    await rejection;

    expect(harness.connection.start).toHaveBeenCalledTimes(1);
    expect(harness.restoreAfterReconnect).not.toHaveBeenCalled();
    expect(harness.onClosed).toHaveBeenCalledTimes(1);
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
    vi.clearAllTimers();
    vi.unstubAllGlobals();
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
  const onClosed = vi.fn(async (): Promise<void> => undefined);
  const onRecovered = vi.fn(async (): Promise<void> => undefined);
  const reportDiagnostic = vi.fn();
  const lifecycle = createSignalRConnectionLifecycle({
    connection: connection as unknown as HubConnection,
    attemptAuthenticationRefresh,
    notifyAuthenticationFailure,
    restoreAfterReconnect,
    onUnavailable,
    onClosed,
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
    onClosed,
    onRecovered,
    reportDiagnostic
  };
}
