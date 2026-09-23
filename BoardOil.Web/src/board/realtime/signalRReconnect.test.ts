import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type HubConnection,
  type ITransport
} from '@microsoft/signalr';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ReconnectHttpClient, signalRReconnectPolicy } from './signalRReconnect';

const connections: HubConnection[] = [];

describe('native SignalR reconnection', () => {
  beforeEach(() => vi.useFakeTimers());

  afterEach(async () => {
    await Promise.all(connections.splice(0).map(connection => connection.stop()));
    vi.unstubAllGlobals();
    vi.useRealTimers();
  });

  it('keeps trying beyond the default retry limit and recovers when the server returns', async () => {
    const harness = createHarness();
    await harness.connection.start();
    harness.setStatus(503);
    harness.drop();

    await vi.advanceTimersByTimeAsync(0);
    expect(harness.fetch).toHaveBeenCalledTimes(2);
    await vi.advanceTimersByTimeAsync(2_000);
    expect(harness.fetch).toHaveBeenCalledTimes(3);
    await vi.advanceTimersByTimeAsync(10_000);
    expect(harness.fetch).toHaveBeenCalledTimes(4);
    for (let attempt = 0; attempt < 8; attempt += 1) {
      await vi.advanceTimersByTimeAsync(30_000);
      expect(harness.fetch).toHaveBeenCalledTimes(5 + attempt);
      expect(harness.connection.state).toBe(HubConnectionState.Reconnecting);
    }

    harness.setStatus(200);
    await vi.advanceTimersByTimeAsync(30_000);

    expect(harness.connection.state).toBe(HubConnectionState.Connected);
    expect(harness.reconnected).toHaveBeenCalledTimes(1);
    expect(harness.refreshSession).not.toHaveBeenCalled();
  });

  it('renews an expired cookie during native reconnect negotiation', async () => {
    const harness = createHarness();
    await harness.connection.start();
    harness.setStatus(401);
    harness.refreshSession.mockImplementation(async () => {
      harness.setStatus(200);
      return true;
    });
    harness.drop();
    await vi.advanceTimersByTimeAsync(0);

    expect(harness.connection.state).toBe(HubConnectionState.Reconnecting);
    expect(harness.fetch).toHaveBeenCalledTimes(2);
    await vi.advanceTimersByTimeAsync(2_000);

    expect(harness.connection.state).toBe(HubConnectionState.Connected);
    expect(harness.refreshSession).toHaveBeenCalledTimes(1);
    expect(harness.fetch).toHaveBeenCalledTimes(3);
    expect(harness.fetch).toHaveBeenLastCalledWith(
      expect.stringContaining('/negotiate?'), expect.objectContaining({ credentials: 'include' }));
  });

  it('continues native retries when session refresh is temporarily unavailable', async () => {
    const harness = createHarness();
    await harness.connection.start();
    harness.setStatus(401);
    harness.drop();
    await vi.advanceTimersByTimeAsync(0);
    await vi.advanceTimersByTimeAsync(2_000);
    await vi.advanceTimersByTimeAsync(10_000);
    await vi.advanceTimersByTimeAsync(60_000);

    expect(harness.connection.state).toBe(HubConnectionState.Reconnecting);
    expect(harness.refreshSession).toHaveBeenCalledTimes(5);
    harness.refreshSession.mockImplementation(async () => {
      harness.setStatus(200);
      return true;
    });
    await vi.advanceTimersByTimeAsync(30_000);
    expect(harness.connection.state).toBe(HubConnectionState.Reconnecting);
    await vi.advanceTimersByTimeAsync(30_000);
    expect(harness.connection.state).toBe(HubConnectionState.Connected);
  });

  it('leaves further unauthorized negotiation attempts to the native retry schedule', async () => {
    const harness = createHarness();
    await harness.connection.start();
    harness.setStatus(401);
    harness.refreshSession.mockResolvedValue(true);
    harness.drop();
    await vi.advanceTimersByTimeAsync(0);

    expect(harness.fetch).toHaveBeenCalledTimes(2);
    expect(harness.refreshSession).toHaveBeenCalledTimes(1);
    expect(harness.connection.state).toBe(HubConnectionState.Reconnecting);
    await vi.advanceTimersByTimeAsync(2_000);
    expect(harness.fetch).toHaveBeenCalledTimes(3);
    expect(harness.refreshSession).toHaveBeenCalledTimes(2);
  });

  it('continues native retries while session refresh remains pending', async () => {
    const harness = createHarness();
    await harness.connection.start();
    const pendingRefresh = new Promise<boolean>(() => undefined);
    harness.refreshSession.mockReturnValue(pendingRefresh);
    harness.setStatus(401);
    harness.drop();
    await vi.advanceTimersByTimeAsync(0);
    await vi.advanceTimersByTimeAsync(2_000);
    await vi.advanceTimersByTimeAsync(10_000);
    await vi.advanceTimersByTimeAsync(30_000);

    expect(harness.connection.state).toBe(HubConnectionState.Reconnecting);
    expect(harness.fetch).toHaveBeenCalledTimes(5);
    expect(harness.refreshSession).toHaveBeenCalledTimes(4);
    await harness.connection.stop();
    expect(harness.connection.state).toBe(HubConnectionState.Disconnected);
  });

  it('cancels scheduled retries when intentionally stopped', async () => {
    const harness = createHarness();
    await harness.connection.start();
    harness.setStatus(503);
    harness.drop();
    await vi.advanceTimersByTimeAsync(0);
    await harness.connection.stop();
    await vi.advanceTimersByTimeAsync(120_000);

    expect(harness.connection.state).toBe(HubConnectionState.Disconnected);
    expect(harness.fetch).toHaveBeenCalledTimes(2);
    expect(harness.reconnected).not.toHaveBeenCalled();
  });

  it('stops while refresh remains pending and ignores its later completion', async () => {
    const harness = createHarness();
    await harness.connection.start();
    let finishRefresh!: (result: boolean) => void;
    harness.refreshSession.mockImplementation(() => new Promise(resolve => {
      finishRefresh = resolve;
    }));
    harness.setStatus(401);
    harness.drop();
    await vi.advanceTimersByTimeAsync(0);
    expect(harness.refreshSession).toHaveBeenCalledTimes(1);

    await harness.connection.stop();
    expect(harness.connection.state).toBe(HubConnectionState.Disconnected);
    await vi.advanceTimersByTimeAsync(120_000);
    expect(harness.fetch).toHaveBeenCalledTimes(2);

    harness.setStatus(200);
    finishRefresh(true);
    await vi.advanceTimersByTimeAsync(120_000);

    expect(harness.connection.state).toBe(HubConnectionState.Disconnected);
    expect(harness.fetch).toHaveBeenCalledTimes(2);
    expect(harness.reconnected).not.toHaveBeenCalled();
  });

  it('leaves initial-start authentication failures to the existing lifecycle', async () => {
    const harness = createHarness();
    harness.setStatus(401);
    await expect(harness.connection.start()).rejects.toThrow('401');
    await vi.advanceTimersByTimeAsync(120_000);

    expect(harness.fetch).toHaveBeenCalledTimes(1);
    expect(harness.refreshSession).not.toHaveBeenCalled();
    expect(harness.connection.state).toBe(HubConnectionState.Disconnected);
  });
});

function createHarness() {
  let status = 200;
  const fetch = vi.fn(async () => new Response(JSON.stringify({
    connectionId: 'test-connection',
    connectionToken: 'test-token',
    negotiateVersion: 1,
    availableTransports: []
  }), { status }));
  vi.stubGlobal('fetch', fetch);

  const transport: ITransport = {
    connect: async () => undefined,
    send: async data => {
      if (typeof data === 'string' && data.includes('"protocol"')) {
        transport.onreceive?.('{}\u001e');
      }
    },
    stop: async () => { transport.onclose?.(); },
    onreceive: null,
    onclose: null
  };
  const refreshSession = vi.fn(async () => false);
  const connection: HubConnection = new HubConnectionBuilder()
    .withUrl('http://localhost/hubs/board', {
      transport,
      httpClient: new ReconnectHttpClient(
        () => connection.state === HubConnectionState.Reconnecting,
        refreshSession)
    })
    .withAutomaticReconnect(signalRReconnectPolicy)
    .configureLogging(LogLevel.None)
    .build();
  connections.push(connection);
  const reconnected = vi.fn();
  connection.onreconnected(reconnected);

  return {
    connection,
    fetch,
    refreshSession,
    reconnected,
    setStatus: (value: number) => { status = value; },
    drop: () => { transport.onclose?.(new Error('Connection lost')); }
  };
}
