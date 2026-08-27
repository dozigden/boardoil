import { HubConnectionState, type HubConnection } from '@microsoft/signalr';

const unauthorizedStartRetryDelaysMs = [0, 1_000, 3_000, 7_000];
const terminalRecoveryRetryDelaysMs = [2_000, 10_000];

type SignalRConnectionLifecycleOptions = {
  connection: HubConnection;
  attemptAuthenticationRefresh: () => Promise<boolean>;
  notifyAuthenticationFailure: () => void;
  restoreAfterReconnect: () => Promise<void>;
  onUnavailable: () => Promise<unknown> | unknown;
  onRecoveryExhausted: () => Promise<unknown> | unknown;
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

  connection.onreconnecting(error => {
    beginReconnectTransition();
    options.log('Connection reconnecting.', {
      error: error instanceof Error ? error.message : String(error)
    });

    if (error) {
      reportDiagnostic('realtime-reconnecting', error);
    }
    void notifyUnavailable();
  });

  connection.onreconnected(async () => {
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
      throw error;
    } finally {
      completeReconnectTransition(reconnectError);
    }
  });

  connection.onclose(async error => {
    beginReconnectTransition();
    let recoveryError: unknown = null;
    options.log('Connection closed.', {
      error: error instanceof Error ? error.message : String(error)
    });

    if (error) {
      reportDiagnostic('realtime-closed', error);
    }

    try {
      if (stopping || options.isSuppressed()) {
        return;
      }

      await notifyUnavailable();
      recoveryError = await recoverAfterTerminalClose();
    } finally {
      completeReconnectTransition(recoveryError);
    }
  });

  async function start() {
    await ensureStarted(true);
  }

  async function ensureStarted(waitForReconnect: boolean) {
    if (waitForReconnect && reconnectTransition) {
      options.log('Waiting for reconnect recovery to finish.');
      await reconnectTransition;
      if (reconnectTransitionError !== null) {
        throw reconnectTransitionError;
      }
      if (!stopping) {
        await ensureStarted(true);
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

  async function recoverAfterTerminalClose() {
    const attemptCount = terminalRecoveryRetryDelaysMs.length + 1;
    let latestError: unknown = null;
    for (let attempt = 0; attempt < attemptCount; attempt += 1) {
      if (stopping || options.isSuppressed()) {
        return null;
      }

      if (attempt > 0) {
        await wait(terminalRecoveryRetryDelaysMs[attempt - 1]!);
        if (stopping || options.isSuppressed()) {
          return null;
        }
      }

      try {
        await ensureStarted(false);
        await options.restoreAfterReconnect();
        if (stopping) {
          return null;
        }

        await options.onRecovered();
        options.log('Realtime recovered after terminal close.', { attempt: attempt + 1 });
        return null;
      } catch (error) {
        latestError = error;
        reportDiagnostic('realtime-recovery-failed', error);
        await notifyUnavailable();
        if (isUnauthorizedNegotiationError(error)) {
          break;
        }
      }
    }

    if (!stopping && !options.isSuppressed()) {
      await options.onRecoveryExhausted();
    }

    return latestError;
  }

  async function stop(beforeStop?: () => Promise<void>) {
    stopping = true;
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
