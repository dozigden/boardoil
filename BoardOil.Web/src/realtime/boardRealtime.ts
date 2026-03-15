import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { computed, ref } from 'vue';
import { boardHubUrl } from '../api/config';
import type { TypingChangedEvent } from '../types/boardTypes';

type BoardChangeHandler = () => Promise<unknown>;

export function createBoardRealtime(onBoardChanged: BoardChangeHandler) {
  const typingByCard = ref<Record<number, Record<string, Set<string>>>>({});
  const typingTimers = new Map<string, ReturnType<typeof setTimeout>>();
  const localUserLabel = resolveLocalUserLabel();
  let hubConnection: HubConnection | null = null;

  const eventNames = [
    'ColumnCreated',
    'ColumnUpdated',
    'ColumnDeleted',
    'CardCreated',
    'CardUpdated',
    'CardDeleted',
    'CardMoved'
  ] as const;

  async function connect() {
    if (hubConnection) {
      return;
    }

    hubConnection = new HubConnectionBuilder()
      .withUrl(boardHubUrl)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    for (const eventName of eventNames) {
      hubConnection.on(eventName, async () => {
        await onBoardChanged();
      });
    }

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
      await onBoardChanged();
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
