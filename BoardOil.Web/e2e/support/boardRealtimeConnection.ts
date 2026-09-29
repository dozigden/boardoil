import type { Page, WebSocketRoute } from '@playwright/test';

export async function interceptBoardConnection(page: Page) {
  let client: WebSocketRoute | null = null;
  let server: WebSocketRoute | null = null;
  await page.routeWebSocket('**/hubs/board?*', route => {
    client = route;
    server = route.connectToServer();
  });

  return {
    async disconnect() {
      if (!client || !server) {
        throw new Error('The board realtime connection has not opened.');
      }
      const currentClient = client;
      const currentServer = server;
      await currentClient.close({ code: 1012, reason: 'Regression test reconnect' });
      await currentServer.close();
    }
  };
}
