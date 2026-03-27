import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { boardHubUrl } from '../api/config';
import type { Card, Column } from '../types/boardTypes';

type RealtimeHandlers = {
  onColumnCreated: (column: Column) => Promise<unknown> | unknown;
  onColumnUpdated: (column: Column) => Promise<unknown> | unknown;
  onColumnDeleted: (columnId: number) => Promise<unknown> | unknown;
  onCardCreated: (card: Card) => Promise<unknown> | unknown;
  onCardUpdated: (card: Card) => Promise<unknown> | unknown;
  onCardDeleted: (cardId: number) => Promise<unknown> | unknown;
  onCardMoved: (card: Card) => Promise<unknown> | unknown;
  onResync: () => Promise<unknown> | unknown;
};

export function createBoardRealtime(handlers: RealtimeHandlers) {
  let hubConnection: HubConnection | null = null;

  async function connect() {
    if (hubConnection) {
      return;
    }

    hubConnection = new HubConnectionBuilder()
      .withUrl(boardHubUrl)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    hubConnection.on('ColumnCreated', async (column: Column) => {
      await handlers.onColumnCreated(column);
    });
    hubConnection.on('ColumnUpdated', async (column: Column) => {
      await handlers.onColumnUpdated(column);
    });
    hubConnection.on('ColumnDeleted', async (columnId: number) => {
      await handlers.onColumnDeleted(columnId);
    });
    hubConnection.on('CardCreated', async (card: Card) => {
      await handlers.onCardCreated(card);
    });
    hubConnection.on('CardUpdated', async (card: Card) => {
      await handlers.onCardUpdated(card);
    });
    hubConnection.on('CardDeleted', async (cardId: number) => {
      await handlers.onCardDeleted(cardId);
    });
    hubConnection.on('CardMoved', async (card: Card) => {
      await handlers.onCardMoved(card);
    });

    hubConnection.onreconnected(async () => {
      await handlers.onResync();
    });

    await hubConnection.start();
  }

  async function disconnect() {
    if (hubConnection) {
      await hubConnection.stop();
      hubConnection = null;
    }
  }

  return {
    connect,
    disconnect,
  };
}
