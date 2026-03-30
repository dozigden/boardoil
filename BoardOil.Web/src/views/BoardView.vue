<template>
  <section v-if="isLoadingBoard" class="board-loading" aria-live="polite">
    <span class="board-loading-indicator" aria-hidden="true" />
    <p class="board-loading-label">Loading board...</p>
  </section>

  <section v-else-if="board" class="board board-view">
    <article
      v-for="column in board.columns"
      :key="column.id"
      class="column"
      @dragover.prevent
      @drop="dropCard(column.id, null)"
    >
      <header class="column-header">
        <h2 class="column-name">{{ column.title }}</h2>
        <button
          type="button"
          class="ghost column-add-card"
          aria-label="Add card"
          title="Add card"
          @click="openNewCardDraft(column.id)"
        >
          <Plus :size="16" aria-hidden="true" />
        </button>
      </header>

      <div class="column-content">
        <article v-if="newCardDraftTitles[column.id] !== undefined" class="card create-card-inline">
          <label class="create-card-inline-label">
            Title
            <input
              :ref="element => setNewCardDraftInput(column.id, element)"
              :value="newCardDraftTitles[column.id]"
              type="text"
              maxlength="200"
              placeholder="New card title"
              @input="updateNewCardDraftTitle(column.id, ($event.target as HTMLInputElement).value)"
              @keydown.enter.prevent="saveNewCardDraft(column.id)"
              @keydown.esc.prevent="closeNewCardDraft(column.id)"
            />
          </label>
          <div class="editor-actions create-card-inline-actions">
            <button type="button" class="create-card-save" aria-label="Save new card" title="Save new card" @click="saveNewCardDraft(column.id)">
              <Check :size="16" aria-hidden="true" />
            </button>
            <button
              type="button"
              class="ghost create-card-cancel"
              aria-label="Cancel new card"
              title="Cancel new card"
              @click="closeNewCardDraft(column.id)"
            >
              <X :size="16" aria-hidden="true" />
            </button>
          </div>
        </article>

        <Card
          v-for="card in column.cards"
          :key="card.id"
          :card="card"
          :column-id="column.id"
          @start-drag="startDrag"
          @drop-card="dropCard"
          @edit-card="openCardEditor"
        />
      </div>
    </article>
  </section>
</template>

<script setup lang="ts">
import { Check, Plus, X } from 'lucide-vue-next';
import { storeToRefs } from 'pinia';
import { nextTick, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import Card from '../components/Card.vue';
import { useBoardStore } from '../stores/boardStore';
import { useTagStore } from '../stores/tagStore';

const newCardDraftTitles = ref<Record<number, string>>({});
const newCardDraftInputs = ref<Record<number, HTMLInputElement | null>>({});

const route = useRoute();
const router = useRouter();
const boardStore = useBoardStore();
const tagStore = useTagStore();
const { board, isLoadingBoard } = storeToRefs(boardStore);
const { createCard, startDrag, dropCard } = boardStore;

async function openNewCardDraft(columnId: number) {
  if (newCardDraftTitles.value[columnId] !== undefined) {
    newCardDraftInputs.value[columnId]?.focus();
    return;
  }

  newCardDraftTitles.value[columnId] = '';
  await nextTick();
  newCardDraftInputs.value[columnId]?.focus();
}

function updateNewCardDraftTitle(columnId: number, value: string) {
  if (newCardDraftTitles.value[columnId] === undefined) {
    return;
  }

  newCardDraftTitles.value[columnId] = value;
}

function closeNewCardDraft(columnId: number) {
  delete newCardDraftTitles.value[columnId];
  delete newCardDraftInputs.value[columnId];
}

function setNewCardDraftInput(columnId: number, element: unknown) {
  newCardDraftInputs.value[columnId] = element instanceof HTMLInputElement ? element : null;
}

async function saveNewCardDraft(columnId: number) {
  const title = newCardDraftTitles.value[columnId] ?? '';
  if (!title.trim()) {
    return;
  }

  await createCard(columnId, title);
  closeNewCardDraft(columnId);
}

async function openCardEditor(cardId: number) {
  const boardId = resolveBoardId();
  if (boardId === null) {
    return;
  }

  await router.push({ name: 'board-card', params: { boardId, cardId } });
}

function resolveBoardId() {
  const parsed = Number.parseInt(String(route.params.boardId ?? ''), 10);
  return Number.isFinite(parsed) ? parsed : null;
}

watch(
  () => route.params.boardId,
  async () => {
    const boardId = resolveBoardId();
    if (boardId === null) {
      await router.replace({ name: 'boards' });
      return;
    }

    const loaded = await boardStore.initialize(boardId);
    if (!loaded && resolveBoardId() === boardId) {
      await router.replace({ name: 'boards' });
      return;
    }

    await tagStore.loadTags(boardId);
  },
  { immediate: true }
);
</script>
