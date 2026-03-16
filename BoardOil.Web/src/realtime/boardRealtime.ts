import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { computed, ref } from 'vue';
import { boardHubUrl } from '../api/config';
import type { Card, Column, TypingChangedEvent } from '../types/boardTypes';

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
  const typingByCard = ref<Record<number, Set<string>>>({});
  const typingTimers = new Map<number, ReturnType<typeof setTimeout>>();
  const localUserLabel = resolveLocalUserLabel();
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

    hubConnection.on('TypingChanged', (event: TypingChangedEvent) => {
      const labels = typingByCard.value[event.cardId] ?? new Set<string>();

      if (event.isTyping) {
        labels.add(event.userLabel);
      } else {
        labels.delete(event.userLabel);
      }

      typingByCard.value[event.cardId] = labels;
    });

    hubConnection.onreconnected(async () => {
      await handlers.onResync();
    });

    await hubConnection.start();
  }

  async function disconnect() {
    for (const timeout of typingTimers.values()) {
      clearTimeout(timeout);
    }
    typingTimers.clear();

    if (hubConnection) {
      await hubConnection.stop();
      hubConnection = null;
    }
  }

  function announceTyping(cardId: number) {
    if (!hubConnection) {
      return;
    }

    void hubConnection.invoke('TypingStarted', cardId, localUserLabel);

    if (typingTimers.has(cardId)) {
      clearTimeout(typingTimers.get(cardId));
    }

    const timeout = setTimeout(() => {
      void stopTyping(cardId);
    }, 1400);

    typingTimers.set(cardId, timeout);
  }

  async function stopTyping(cardId: number) {
    if (!hubConnection) {
      return;
    }

    if (typingTimers.has(cardId)) {
      clearTimeout(typingTimers.get(cardId));
      typingTimers.delete(cardId);
    }

    await hubConnection.invoke('TypingStopped', cardId, localUserLabel);
  }

  const typingSummary = computed(() => {
    return (cardId: number) => {
      const cardTyping = typingByCard.value[cardId];
      if (!cardTyping) {
        return false;
      }

      return Array.from(cardTyping).some(label => label !== localUserLabel);
    };
  });

  return {
    connect,
    disconnect,
    announceTyping,
    stopTyping,
    typingSummary
  };
}

function resolveLocalUserLabel() {
  const existing = localStorage.getItem('boardoil.userLabel');
  if (existing) {
    return existing;
  }

  const generated = `User-${Math.floor(1000 + Math.random() * 9000)}`;
  localStorage.setItem('boardoil.userLabel', generated);
  return generated;
}
