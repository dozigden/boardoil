import type { Page, WebSocketRoute } from '@playwright/test';

export async function interceptBoardConnection(page: Page) {
  let client: WebSocketRoute | null = null;
  let server: WebSocketRoute | null = null;
  let eventsPaused = false;
  let buffered: string[] | null = null;
  await page.routeWebSocket('**/hubs/board?*', route => {
    client = route;
    server = route.connectToServer();
    server.onMessage(message => {
      if (buffered && typeof message === 'string') {
        buffered.push(message);
      } else if (!eventsPaused) {
        route.send(message);
      }
    });
  });

  return {
    pauseEvents() {
      eventsPaused = true;
    },
    bufferEvents() {
      buffered = [];
    },
    get bufferedEventCount() {
      return (buffered ?? []).flatMap(message => message.split('\u001e'))
        .filter(record => record && JSON.parse(record).type === 1).length;
    },
    releaseEvents() {
      if (!client || !buffered) {
        throw new Error('The board realtime connection is not buffering events.');
      }
      const messages = buffered;
      buffered = null;
      // One SignalR frame makes the handlers update Vue in the same render.
      client.send(messages.join(''));
    },
    async disconnect() {
      if (!client || !server) {
        throw new Error('The board realtime connection has not opened.');
      }
      const currentClient = client;
      const currentServer = server;
      eventsPaused = false;
      buffered = null;
      await currentClient.close({ code: 1012, reason: 'Regression test reconnect' });
      await currentServer.close();
    }
  };
}
