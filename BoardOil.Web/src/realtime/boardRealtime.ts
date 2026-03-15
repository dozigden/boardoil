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
  const typingByCard = ref<Record<number, Record<string, Set<string>>>>({});
  const typingTimers = new Map<string, ReturnType<typeof setTimeout>>();
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
      const cardTyping = typingByCard.value[event.cardId] ?? {};
      const fieldSet = cardTyping[event.field] ?? new Set<string>();

      if (event.isTyping) {
        fieldSet.add(event.userLabel);
      } else {
        fieldSet.delete(event.userLabel);
      }

      cardTyping[event.field] = fieldSet;
      typingByCard.value[event.cardId] = cardTyping;
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

  function announceTyping(cardId: number, field: string) {
    if (!hubConnection) {
      return;
    }

    const key = `${cardId}:${field}`;
    void hubConnection.invoke('TypingStarted', cardId, field, localUserLabel);

    if (typingTimers.has(key)) {
      clearTimeout(typingTimers.get(key));
    }

    const timeout = setTimeout(() => {
      void stopTyping(cardId, field);
    }, 1400);

    typingTimers.set(key, timeout);
  }

  async function stopTyping(cardId: number, field: string) {
    if (!hubConnection) {
      return;
    }

    const key = `${cardId}:${field}`;
    if (typingTimers.has(key)) {
      clearTimeout(typingTimers.get(key));
      typingTimers.delete(key);
    }

    await hubConnection.invoke('TypingStopped', cardId, field, localUserLabel);
  }

  const typingSummary = computed(() => {
    return (cardId: number) => {
      const cardTyping = typingByCard.value[cardId];
      if (!cardTyping) {
        return [] as string[];
      }

      return Object.entries(cardTyping)
        .flatMap(([field, labels]) =>
          Array.from(labels)
            .filter(label => label !== localUserLabel)
            .map(label => `${label} (${field})`)
        )
        .sort((a, b) => a.localeCompare(b));
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
