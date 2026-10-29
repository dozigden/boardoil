import { HubConnectionState, type HubConnection } from '@microsoft/signalr';

const unauthorizedStartRetryDelaysMs = [0, 1_000, 3_000, 7_000];
const visibleOutageDelayMs = 60_000;

type SignalRConnectionLifecycleOptions = {
  connection: HubConnection;
  attemptAuthenticationRefresh: () => Promise<boolean>;
  notifyAuthenticationFailure: () => void;
  restoreAfterReconnect: () => Promise<void>;
  onUnavailable: () => Promise<unknown> | unknown;
  onClosed: () => Promise<unknown> | unknown;
  onRecovered: () => Promise<unknown> | unknown;
  reportDiagnostic: (phase: string, error: unknown, connection: HubConnection) => void;
  isSuppressed: () => boolean;
  log: (message: string, details?: unknown) => void;
};

export type SignalRConnectionLifecycle = {
  start: () => Promise<void>;
  stop: (beforeStop?: () => Promise<void>) => Promise<void>;
};

export function createSignalRConnectionLifecycle(
  options: SignalRConnectionLifecycleOptions
): SignalRConnectionLifecycle {
  const { connection } = options;
  let startPromise: Promise<void> | null = null;
  let reconnectTransition: Promise<void> | null = null;
  let finishReconnectTransition: (() => void) | null = null;
  let reconnectTransitionError: unknown = null;
  let stopping = false;
  const documentRef = globalThis.document;
  let outage: { error: Error; reported: boolean } | null = null;
  let outageTimer: ReturnType<typeof setTimeout> | null = null;

  connection.onreconnecting(error => {
    beginReconnectTransition();
    options.log('Connection reconnecting.', {
      error: error instanceof Error ? error.message : String(error)
    });

    delayOutageReport(error ?? new Error('Realtime connection interrupted.'));
    void notifyUnavailable();
  });

  connection.onreconnected(async () => {
    cancelOutageReport();
    let reconnectError: unknown = null;
    try {
      if (stopping) {
        return;
      }

      options.log('Connection reconnected.');
      await options.restoreAfterReconnect();
      if (stopping) {
        return;
      }

      await options.onRecovered();
      options.log('State restored after reconnect.');
    } catch (error) {
      reconnectError = error;
      // SignalR does not await async callbacks; report restoration errors here.
      reportDiagnostic('realtime-restore-failed', error);
    } finally {
      completeReconnectTransition(reconnectError);
    }
  });

  connection.onclose(async error => {
    options.log('Connection closed.', {
      error: error instanceof Error ? error.message : String(error)
    });

    delayOutageReport(error ?? new Error('Realtime connection closed.'));

    try {
      if (stopping || options.isSuppressed()) {
        return;
      }

      await options.onClosed();
    } finally {
      completeReconnectTransition(error ?? new Error('Realtime connection closed.'));
    }
  });

  async function start() {
    await ensureStarted();
  }

  async function ensureStarted() {
    if (reconnectTransition) {
      options.log('Waiting for reconnect recovery to finish.');
      await reconnectTransition;
      if (reconnectTransitionError !== null) {
        throw reconnectTransitionError;
      }
      if (!stopping) {
        await ensureStarted();
      }
      return;
    }

    if (connection.state === HubConnectionState.Connected) {
      return;
    }

    if (startPromise) {
      options.log('Waiting for existing connection start.');
      await startPromise;
      return;
    }

    if (connection.state !== HubConnectionState.Disconnected) {
      throw new Error(`Cannot start realtime while the connection state is '${connection.state}'.`);
    }

    options.log('Starting realtime connection.');
    startPromise = startConnection().finally(() => {
      startPromise = null;
    });

    await startPromise;
    cancelOutageReport();
    options.log('Realtime connection started.');
  }

  async function startConnection() {
    try {
      await connection.start();
    } catch (error) {
      if (!isUnauthorizedNegotiationError(error)) {
        reportDiagnostic('realtime-start-failed', error);
        await notifyUnavailable();
        throw error;
      }

      try {
        await retryUnauthorizedStart(error);
      } catch (retryError) {
        if (!isUnauthorizedNegotiationError(retryError)) {
          reportDiagnostic('realtime-start-failed', retryError);
        }

        throw retryError;
      }
    }
  }

  async function retryUnauthorizedStart(initialError: unknown) {
    let latestUnauthorizedError = initialError;

    for (const delayMs of unauthorizedStartRetryDelaysMs) {
      if (delayMs > 0) {
        options.log('Waiting before unauthorized realtime retry.', { delayMs });
        await wait(delayMs);
      }

      options.log('Attempting session refresh before realtime retry.');
      const refreshed = await options.attemptAuthenticationRefresh();
      if (!refreshed) {
        options.log('Session refresh failed before realtime retry.');
        continue;
      }

      try {
        options.log('Retrying realtime start after session refresh.');
        await connection.start();
        return;
      } catch (retryError) {
        if (!isUnauthorizedNegotiationError(retryError)) {
          throw retryError;
        }

        latestUnauthorizedError = retryError;
        options.log('Realtime retry still unauthorized.');
      }
    }

    options.notifyAuthenticationFailure();
    throw latestUnauthorizedError;
  }

  async function stop(beforeStop?: () => Promise<void>) {
    stopping = true;
    cancelOutageReport();
    const pendingStart = startPromise;
    if (pendingStart) {
      try {
        options.log('Waiting for pending start before disconnect.');
        await pendingStart;
      } catch {
        // If startup failed, continue teardown.
      }
    }

    await beforeStop?.();
    await connection.stop();
    options.log('Connection stopped.');
  }

  function reportDiagnostic(phase: string, error: unknown) {
    if (stopping || options.isSuppressed() || isUnauthorizedNegotiationError(error)) {
      return;
    }

    options.reportDiagnostic(phase, error, connection);
  }

  function delayOutageReport(error: Error) {
    if (outage || stopping || options.isSuppressed() || isUnauthorizedNegotiationError(error)) {
      return;
    }
    outage = { error, reported: false };
    documentRef?.addEventListener('visibilitychange', scheduleOutageReport);
    scheduleOutageReport();
  }

  function scheduleOutageReport() {
    if (outageTimer !== null) {
      clearTimeout(outageTimer);
      outageTimer = null;
    }
    if (!outage || outage.reported || documentRef?.visibilityState === 'hidden') {
      return;
    }
    // A hidden tab gets a fresh grace period when it becomes visible again.
    outageTimer = setTimeout(() => {
      outageTimer = null;
      if (!outage || documentRef?.visibilityState === 'hidden' || stopping || options.isSuppressed()) {
        return;
      }
      outage.reported = true;
      reportDiagnostic('realtime-outage', outage.error);
    }, visibleOutageDelayMs);
  }

  function cancelOutageReport() {
    if (outageTimer !== null) {
      clearTimeout(outageTimer);
      outageTimer = null;
    }
    outage = null;
    documentRef?.removeEventListener('visibilitychange', scheduleOutageReport);
  }

  function notifyUnavailable() {
    if (stopping || options.isSuppressed()) {
      options.log('Suppressing realtime warning.');
      return;
    }

    return options.onUnavailable();
  }

  function beginReconnectTransition() {
    if (reconnectTransition) {
      return;
    }

    reconnectTransition = new Promise(resolve => {
      finishReconnectTransition = resolve;
    });
    reconnectTransitionError = null;
  }

  function completeReconnectTransition(error: unknown = null) {
    reconnectTransitionError = error;
    finishReconnectTransition?.();
    finishReconnectTransition = null;
    reconnectTransition = null;
  }

  return {
    start,
    stop
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

async function wait(delayMs: number) {
  await new Promise(resolve => {
    setTimeout(resolve, delayMs);
  });
}
