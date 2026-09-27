import { beforeEach, describe, expect, it, vi } from 'vitest';

const fake = vi.hoisted(() => ({
  eventHandlers: new Map<string, (id: number | null) => void>(),
  lifecycleOptions: null as null | {
    notifyAuthenticationFailure: () => void;
    onUnavailable: () => void;
    onClosed: () => void;
    onRecovered: () => Promise<unknown>;
    isSuppressed: () => boolean;
  },
  start: vi.fn(async (): Promise<void> => undefined),
  stop: vi.fn(async (): Promise<void> => undefined),
  notifyUnauthorized: vi.fn(),
  reportDiagnostic: vi.fn()
}));

vi.mock('@microsoft/signalr', () => ({
  HubConnectionState: { Reconnecting: 'Reconnecting' },
  LogLevel: { Warning: 3 },
  HubConnectionBuilder: class {
    withUrl() { return this; }
    withAutomaticReconnect() { return this; }
    configureLogging() { return this; }
    build() { return { state: 'Disconnected', on: (event: string, handler: (id: number | null) => void) => {
      fake.eventHandlers.set(event, handler);
    } }; }
  }
}));
vi.mock('../../shared/api/config', () => ({ apiBase: 'http://localhost' }));
vi.mock('../../shared/api/http', () => ({
  attemptSessionRefresh: vi.fn(async () => true),
  notifyUnauthorized: fake.notifyUnauthorized
}));
vi.mock('../../shared/errors/clientErrorReporter', () => ({
  clientErrorReporter: { reportRealtimeDiagnostic: fake.reportDiagnostic }
}));
vi.mock('../../board/realtime/signalRReconnect', () => ({
  ReconnectHttpClient: class {},
  signalRReconnectPolicy: {}
}));
vi.mock('../../board/realtime/signalRConnectionLifecycle', () => ({
  createSignalRConnectionLifecycle: (options: typeof fake.lifecycleOptions) => {
    fake.lifecycleOptions = options;
    return { start: fake.start, stop: fake.stop };
  }
}));

import { createSystemJobsRealtime } from './systemJobsRealtime';

beforeEach(() => {
  fake.eventHandlers.clear();
  fake.lifecycleOptions = null;
  fake.start.mockReset().mockResolvedValue(undefined);
  fake.stop.mockReset().mockResolvedValue(undefined);
  fake.notifyUnauthorized.mockClear();
  fake.reportDiagnostic.mockClear();
});

describe('system jobs realtime', () => {
  it('suppresses job callbacks, warnings, recovery and late auth failure after leaving', async () => {
    let finishStart!: () => void;
    fake.start.mockReturnValueOnce(new Promise<void>(resolve => { finishStart = resolve; }));
    const changed = vi.fn();
    const recovered = vi.fn();
    const warning = vi.fn();
    const realtime = createSystemJobsRealtime({ changed, recovered, warning });

    const connecting = realtime.connect();
    const options = fake.lifecycleOptions!;
    expect(options.isSuppressed()).toBe(false);
    realtime.suspend();
    expect(options.isSuppressed()).toBe(true);
    fake.eventHandlers.get('JobsChanged')?.(5);
    options.notifyAuthenticationFailure();
    options.onUnavailable();
    options.onClosed();
    await options.onRecovered();
    finishStart();
    await connecting;
    await realtime.disconnect();

    expect(changed).not.toHaveBeenCalled();
    expect(recovered).not.toHaveBeenCalled();
    expect(warning).not.toHaveBeenCalled();
    expect(fake.notifyUnauthorized).not.toHaveBeenCalled();
    expect(fake.stop).toHaveBeenCalledTimes(1);
  });

  it('does not clear a warning after recovery finishes outside the jobs area', async () => {
    let finishRecovery!: () => void;
    const recovered = vi.fn(() => new Promise<void>(resolve => { finishRecovery = resolve; }));
    const warning = vi.fn();
    const realtime = createSystemJobsRealtime({ changed: vi.fn(), recovered, warning });
    await realtime.connect();

    const recovery = fake.lifecycleOptions!.onRecovered();
    realtime.suspend();
    finishRecovery();
    await recovery;

    expect(recovered).toHaveBeenCalledTimes(1);
    expect(warning).not.toHaveBeenCalled();
    await realtime.disconnect();
  });
});
