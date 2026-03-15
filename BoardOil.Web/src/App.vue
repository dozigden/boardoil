<template>
  <main class="app-shell">
    <header class="app-header">
      <h1>BoardOil</h1>
      <p>Live board for small trusted teams.</p>
      <nav class="view-switch">
        <button
          :class="{ active: currentView === 'board' }"
          type="button"
          @click="goToView('board')"
        >
          Board
        </button>
        <button
          :class="{ active: currentView === 'columns' }"
          type="button"
          @click="goToView('columns')"
        >
          Manage Columns
        </button>
      </nav>
    </header>

    <section v-if="currentView === 'columns'" class="toolbar">
      <form class="create-column" @submit.prevent="createColumn">
        <input v-model="newColumnTitle" type="text" maxlength="200" placeholder="New column title" />
        <button type="submit" :disabled="busy">Add Column</button>
      </form>
    </section>

    <p v-if="errorMessage" class="error">{{ errorMessage }}</p>

    <section v-if="board && currentView === 'board'" class="board">
      <article
        v-for="column in board.columns"
        :key="column.id"
        class="column"
        @dragover.prevent
        @drop="dropCard(column.id, column.cards.length)"
      >
        <header class="column-header">
          <h2 class="column-name">{{ column.title }}</h2>
        </header>

        <div
          v-for="(card, index) in column.cards"
          :key="card.id"
          class="card"
          draggable="true"
          @dragstart="startDrag(card.id, column.id)"
          @dragover.prevent
          @drop="dropCard(column.id, index)"
        >
          <div class="card-header">
            <strong>{{ card.title }}</strong>
            <button class="ghost" @click="toggleCardEditor(card.id)">
              {{ editingCardId === card.id ? 'Close' : 'Edit' }}
            </button>
          </div>

          <p class="description">{{ card.description }}</p>

          <div v-if="typingSummary(card.id).length > 0" class="typing">
            Typing: {{ typingSummary(card.id).join(', ') }}
          </div>

          <div v-if="editingCardId === card.id" class="editor">
            <label>
              Title
              <input
                :value="cardDrafts[card.id]?.title ?? card.title"
                maxlength="200"
                @focus="announceTyping(card.id, 'title')"
                @blur="stopTyping(card.id, 'title')"
                @input="updateCardDraft(card.id, 'title', ($event.target as HTMLInputElement).value)"
              />
            </label>

            <label>
              Description
              <textarea
                :value="cardDrafts[card.id]?.description ?? card.description"
                maxlength="5000"
                @focus="announceTyping(card.id, 'description')"
                @blur="stopTyping(card.id, 'description')"
                @input="updateCardDraft(card.id, 'description', ($event.target as HTMLTextAreaElement).value)"
              />
            </label>

            <div class="editor-actions">
              <button @click="saveCard(card.id)">Save</button>
              <button class="danger" @click="deleteCard(card.id)">Delete</button>
            </div>
          </div>
        </div>

        <form class="create-card" @submit.prevent="createCard(column.id)">
          <input
            :value="newCardTitles[column.id] ?? ''"
            type="text"
            maxlength="200"
            placeholder="New card title"
            @input="updateNewCardTitle(column.id, ($event.target as HTMLInputElement).value)"
          />
          <button type="submit" :disabled="busy">Add Card</button>
        </form>
      </article>
    </section>

    <section v-if="board && currentView === 'columns'" class="column-manager">
      <article v-for="column in board.columns" :key="column.id" class="column-manager-item">
        <label>
          Column title
          <input
            class="column-title"
            :value="columnTitleDrafts[column.id] ?? column.title"
            maxlength="200"
            @input="updateColumnDraft(column.id, ($event.target as HTMLInputElement).value)"
          />
        </label>
        <div class="column-actions">
          <button @click="saveColumn(column.id)">Save</button>
          <button class="danger" @click="deleteColumn(column.id)">Delete</button>
        </div>
      </article>
    </section>
  </main>
</template>

<script setup lang="ts">
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { computed, onMounted, onUnmounted, ref } from 'vue';

type Card = {
  id: number;
  boardColumnId: number;
  title: string;
  description: string;
  position: number;
  createdAtUtc: string;
  updatedAtUtc: string;
};

type Column = {
  id: number;
  title: string;
  position: number;
  createdAtUtc: string;
  updatedAtUtc: string;
  cards: Card[];
};

type Board = {
  id: number;
  name: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  columns: Column[];
};

type ApiEnvelope<T> = {
  success: boolean;
  data: T | null;
  statusCode: number;
  message?: string;
};

type TypingChangedEvent = {
  cardId: number;
  field: string;
  userLabel: string;
  isTyping: boolean;
  expiresAtUtc: string;
};

type ViewMode = 'board' | 'columns';

const configuredApiBase = (import.meta.env.VITE_API_BASE as string | undefined)?.trim() ?? '';
const apiBase = configuredApiBase
  ? configuredApiBase.replace(/\/+$/, '')
  : window.location.origin;
const board = ref<Board | null>(null);
const currentView = ref<ViewMode>(parseViewFromHash(window.location.hash));
const busy = ref(false);
const errorMessage = ref('');
const newColumnTitle = ref('');
const newCardTitles = ref<Record<number, string>>({});
const columnTitleDrafts = ref<Record<number, string>>({});
const cardDrafts = ref<Record<number, { title: string; description: string }>>({});
const editingCardId = ref<number | null>(null);

const typingByCard = ref<Record<number, Record<string, Set<string>>>>({});
const typingTimers = new Map<string, ReturnType<typeof setTimeout>>();

const localUserLabel = (() => {
  const existing = localStorage.getItem('boardoil.userLabel');
  if (existing) {
    return existing;
  }

  const generated = `User-${Math.floor(1000 + Math.random() * 9000)}`;
  localStorage.setItem('boardoil.userLabel', generated);
  return generated;
})();

let hubConnection: HubConnection | null = null;
let dragState: { cardId: number; fromColumnId: number } | null = null;

const eventNames = [
  'ColumnCreated',
  'ColumnUpdated',
  'ColumnDeleted',
  'CardCreated',
  'CardUpdated',
  'CardDeleted',
  'CardMoved'
] as const;

onMounted(async () => {
  window.addEventListener('hashchange', syncViewFromHash);

  try {
    await loadBoard();
    await connectHub();
  } catch (error) {
    errorMessage.value = toMessage(error);
  }
});

onUnmounted(async () => {
  window.removeEventListener('hashchange', syncViewFromHash);

  for (const timeout of typingTimers.values()) {
    clearTimeout(timeout);
  }

  typingTimers.clear();
  if (hubConnection) {
    await hubConnection.stop();
  }
});

function parseViewFromHash(hash: string): ViewMode {
  return hash === '#columns' ? 'columns' : 'board';
}

function syncViewFromHash() {
  currentView.value = parseViewFromHash(window.location.hash);
}

function goToView(view: ViewMode) {
  const hash = view === 'columns' ? '#columns' : '#board';
  if (window.location.hash !== hash) {
    window.location.hash = hash;
  } else {
    currentView.value = view;
  }
}

async function loadBoard() {
  try {
    const response = await fetch(`${apiBase}/api/board`);
    const envelope = (await response.json()) as ApiEnvelope<Board>;

    if (!envelope.success || !envelope.data) {
      errorMessage.value = envelope.message ?? 'Failed to load board.';
      return;
    }

    board.value = normalizeBoard(envelope.data);
    errorMessage.value = '';
  } catch (error) {
    throw new Error(`Cannot reach API at ${apiBase}. Start backend there or set VITE_API_BASE.`);
  }
}

async function createColumn() {
  if (!newColumnTitle.value.trim()) {
    return;
  }

  busy.value = true;
  try {
    await postJson('/api/columns', { title: newColumnTitle.value, position: null });
    newColumnTitle.value = '';
    await loadBoard();
  } catch (error) {
    errorMessage.value = toMessage(error);
  } finally {
    busy.value = false;
  }
}

function updateColumnDraft(columnId: number, value: string) {
  columnTitleDrafts.value[columnId] = value;
}

async function saveColumn(columnId: number) {
  const title = columnTitleDrafts.value[columnId];
  if (title === undefined) {
    return;
  }

  busy.value = true;
  try {
    await patchJson(`/api/columns/${columnId}`, { title, position: null });
  } catch (error) {
    errorMessage.value = toMessage(error);
  } finally {
    busy.value = false;
  }
}

async function deleteColumn(columnId: number) {
  busy.value = true;
  try {
    await deleteJson(`/api/columns/${columnId}`);
    await loadBoard();
  } catch (error) {
    errorMessage.value = toMessage(error);
  } finally {
    busy.value = false;
  }
}

function updateNewCardTitle(columnId: number, value: string) {
  newCardTitles.value[columnId] = value;
}

async function createCard(columnId: number) {
  const title = (newCardTitles.value[columnId] ?? '').trim();
  if (!title) {
    return;
  }

  busy.value = true;
  try {
    await postJson('/api/cards', {
      boardColumnId: columnId,
      title,
      description: '',
      position: null
    });
    newCardTitles.value[columnId] = '';
    await loadBoard();
  } catch (error) {
    errorMessage.value = toMessage(error);
  } finally {
    busy.value = false;
  }
}

function toggleCardEditor(cardId: number) {
  if (editingCardId.value === cardId) {
    editingCardId.value = null;
    return;
  }

  const card = findCard(cardId);
  if (!card) {
    return;
  }

  cardDrafts.value[cardId] = { title: card.title, description: card.description };
  editingCardId.value = cardId;
}

function updateCardDraft(cardId: number, field: 'title' | 'description', value: string) {
  const existing = cardDrafts.value[cardId] ?? { title: '', description: '' };
  cardDrafts.value[cardId] = { ...existing, [field]: value };
  announceTyping(cardId, field);
}

async function saveCard(cardId: number) {
  const draft = cardDrafts.value[cardId];
  if (!draft) {
    return;
  }

  busy.value = true;
  try {
    await patchJson(`/api/cards/${cardId}`, {
      boardColumnId: null,
      title: draft.title,
      description: draft.description,
      position: null
    });
    stopTyping(cardId, 'title');
    stopTyping(cardId, 'description');
    editingCardId.value = null;
  } catch (error) {
    errorMessage.value = toMessage(error);
  } finally {
    busy.value = false;
  }
}

async function deleteCard(cardId: number) {
  busy.value = true;
  try {
    await deleteJson(`/api/cards/${cardId}`);
    await loadBoard();
  } catch (error) {
    errorMessage.value = toMessage(error);
  } finally {
    busy.value = false;
  }
}

function startDrag(cardId: number, fromColumnId: number) {
  dragState = { cardId, fromColumnId };
}

async function dropCard(targetColumnId: number, position: number) {
  if (!dragState) {
    return;
  }

  const movingCardId = dragState.cardId;
  dragState = null;

  busy.value = true;
  try {
    await patchJson(`/api/cards/${movingCardId}`, {
      boardColumnId: targetColumnId,
      title: null,
      description: null,
      position
    });
  } catch (error) {
    errorMessage.value = toMessage(error);
  } finally {
    busy.value = false;
  }
}

async function connectHub() {
  const hubUrl = `${apiBase}/hubs/board`;
  hubConnection = new HubConnectionBuilder()
    .withUrl(hubUrl)
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();

  for (const eventName of eventNames) {
    hubConnection.on(eventName, async () => {
      await loadBoard();
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
    await loadBoard();
  });

  await hubConnection.start();
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

function findCard(cardId: number) {
  return board.value?.columns.flatMap(x => x.cards).find(x => x.id === cardId) ?? null;
}

function normalizeBoard(source: Board): Board {
  return {
    ...source,
    columns: [...source.columns]
      .sort((a, b) => a.position - b.position)
      .map(column => ({
        ...column,
        cards: [...column.cards].sort((a, b) => a.position - b.position)
      }))
  };
}

async function postJson(path: string, payload: unknown) {
  const response = await fetch(`${apiBase}${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload)
  });

  await ensureOk(response);
}

async function patchJson(path: string, payload: unknown) {
  const response = await fetch(`${apiBase}${path}`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload)
  });

  await ensureOk(response);
}

async function deleteJson(path: string) {
  const response = await fetch(`${apiBase}${path}`, {
    method: 'DELETE'
  });

  await ensureOk(response);
}

async function ensureOk(response: Response) {
  const body = (await response.json().catch(() => null)) as ApiEnvelope<unknown> | null;
  if (response.ok && body?.success !== false) {
    return;
  }

  throw new Error(body?.message ?? `Request failed with status ${response.status}`);
}

function toMessage(error: unknown): string {
  if (error instanceof Error) {
    return error.message;
  }

  return 'Unexpected error.';
}
</script>
