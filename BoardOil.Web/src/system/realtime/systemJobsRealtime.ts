import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr';
import { apiBase } from '../../shared/api/config';
import { attemptSessionRefresh, notifyUnauthorized } from '../../shared/api/http';
import { clientErrorReporter } from '../../shared/errors/clientErrorReporter';
import { createSignalRConnectionLifecycle, type SignalRConnectionLifecycle } from '../../board/realtime/signalRConnectionLifecycle';
import { ReconnectHttpClient, signalRReconnectPolicy } from '../../board/realtime/signalRReconnect';

export type SystemJobsRealtime = {
  connect: () => Promise<void>;
  suspend: () => void;
  disconnect: () => Promise<void>;
};
export type SystemJobsRealtimeHandlers = {
  changed: (id: number | null) => Promise<unknown> | unknown;
  recovered: () => Promise<unknown> | unknown;
  warning?: (message: string | null) => void;
};

export function createSystemJobsRealtime(handlers: SystemJobsRealtimeHandlers): SystemJobsRealtime {
  let connection: HubConnection | null = null;
  let lifecycle: SignalRConnectionLifecycle | null = null;
  let active = false;

  async function connect() {
    active = true;
    if (!connection) {
      let next: HubConnection;
      next = new HubConnectionBuilder()
        .withUrl(`${apiBase}/hubs/system-jobs`, {
          httpClient: new ReconnectHttpClient(
            () => next.state === HubConnectionState.Reconnecting,
            attemptSessionRefresh
          )
        })
        .withAutomaticReconnect(signalRReconnectPolicy)
        .configureLogging(LogLevel.Warning)
        .build();
      next.on('JobsChanged', (id: number | null) => {
        if (active) void handlers.changed(id);
      });
      connection = next;
      lifecycle = createSignalRConnectionLifecycle({
        connection: next,
        attemptAuthenticationRefresh: attemptSessionRefresh,
        notifyAuthenticationFailure: () => { if (active) notifyUnauthorized(); },
        restoreAfterReconnect: async () => undefined,
        onUnavailable: () => { if (active) handlers.warning?.('Job updates are reconnecting. Use Refresh if needed.'); },
        onClosed: () => { if (active) handlers.warning?.('Live job updates are unavailable. Use Refresh if needed.'); },
        onRecovered: async () => {
          if (active) {
            await handlers.recovered();
            if (active) handlers.warning?.(null);
          }
        },
        reportDiagnostic: (phase, error, current) => {
          if (active) {
            void clientErrorReporter.reportRealtimeDiagnostic(phase, error,
              { area: 'system-jobs', connectionState: current.state });
          }
        },
        isSuppressed: () => !active,
        log: () => undefined
      });
    }
    await lifecycle!.start();
  }

  async function disconnect() {
    suspend();
    const current = lifecycle;
    lifecycle = null;
    connection = null;
    await current?.stop();
  }

  function suspend() { active = false; }

  return { connect, suspend, disconnect };
}
