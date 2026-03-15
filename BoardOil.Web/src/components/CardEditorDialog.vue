<template>
  <dialog v-if="editingCard" ref="dialogRef" class="card-modal" @cancel.prevent="closeCardEditor" @click="onDialogClick">
    <form class="editor card-modal-content" @submit.prevent="saveCard">
      <button type="button" class="ghost card-modal-close" aria-label="Cancel editing" title="Cancel" @click="closeCardEditor">
        <X :size="18" aria-hidden="true" />
      </button>
      <h3 class="card-modal-title">{{ editingCard.id }}</h3>
      <label>
        Title
        <input
          :value="cardDraft?.title ?? editingCard.title"
          maxlength="200"
          @focus="announceEditingCardTyping('title')"
          @blur="stopEditingCardTyping('title')"
          @input="updateEditingCardDraft('title', ($event.target as HTMLInputElement).value)"
        />
      </label>

      <label>
        Description
        <textarea
          :value="cardDraft?.description ?? editingCard.description"
          maxlength="5000"
          @focus="announceEditingCardTyping('description')"
          @blur="stopEditingCardTyping('description')"
          @input="updateEditingCardDraft('description', ($event.target as HTMLTextAreaElement).value)"
        />
      </label>

      <div class="editor-actions card-modal-actions">
        <button type="button" class="danger card-modal-delete" aria-label="Delete card" title="Delete card" @click="deleteEditingCard">
          <Trash2 :size="16" aria-hidden="true" />
        </button>
        <div class="card-modal-actions-left">
          <button type="submit" class="card-modal-save" aria-label="Save card" title="Save card">
            <Check :size="16" aria-hidden="true" />
            <span>Save</span>
          </button>
          <button type="button" class="ghost card-modal-cancel" aria-label="Cancel editing" title="Cancel" @click="closeCardEditor">
            <X :size="16" aria-hidden="true" />
            <span>Cancel</span>
          </button>
        </div>
      </div>
    </form>
  </dialog>
</template>

<script setup lang="ts">
import { Check, Trash2, X } from 'lucide-vue-next';
import { storeToRefs } from 'pinia';
import { computed, nextTick, onBeforeUnmount, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useBoardStore } from '../stores/boardStore';

const dialogRef = ref<HTMLDialogElement | null>(null);
const route = useRoute();
const router = useRouter();
const boardStore = useBoardStore();
const { board } = storeToRefs(boardStore);
const { saveCard: saveCardAction, deleteCard, announceTyping, stopTyping } = boardStore;
const cardDraft = ref<{ title: string; description: string } | null>(null);

const routeCardId = computed<number | null>(() => {
  const raw = route.params.cardId;
  const parsed = typeof raw === 'string' ? Number.parseInt(raw, 10) : Number.NaN;
  return Number.isFinite(parsed) ? parsed : null;
});

const editingCard = computed(() => boardStore.getCardById(routeCardId.value));

function onDialogClick(event: MouseEvent) {
  if (event.target === dialogRef.value) {
    void closeCardEditor();
  }
}

async function syncDialogState() {
  await nextTick();
  const dialog = dialogRef.value;
  if (!dialog || dialog.open) {
    return;
  }

  dialog.showModal();
}

function stopTypingForCard(cardId: number) {
  stopTyping(cardId, 'title');
  stopTyping(cardId, 'description');
}

async function closeCardEditor() {
  const cardId = routeCardId.value;
  if (cardId !== null) {
    stopTypingForCard(cardId);
  }

  await router.push({ name: 'board' });
}

function updateEditingCardDraft(field: 'title' | 'description', value: string) {
  if (!cardDraft.value) {
    return;
  }

  cardDraft.value = { ...cardDraft.value, [field]: value };
}

function announceEditingCardTyping(field: 'title' | 'description') {
  const cardId = routeCardId.value;
  if (cardId === null) {
    return;
  }

  announceTyping(cardId, field);
}

function stopEditingCardTyping(field: 'title' | 'description') {
  const cardId = routeCardId.value;
  if (cardId === null) {
    return;
  }

  stopTyping(cardId, field);
}

async function saveCard() {
  const cardId = routeCardId.value;
  if (cardId === null || !cardDraft.value) {
    return;
  }

  await saveCardAction(cardId, cardDraft.value.title, cardDraft.value.description);
  await closeCardEditor();
}

async function deleteEditingCard() {
  const cardId = routeCardId.value;
  if (cardId === null) {
    return;
  }

  await deleteCard(cardId);
  await closeCardEditor();
}

watch(
  routeCardId,
  (nextCardId, previousCardId) => {
    if (previousCardId !== null && previousCardId !== nextCardId) {
      stopTypingForCard(previousCardId);
    }
  }
);

watch(
  [routeCardId, editingCard, board],
  ([nextCardId, nextCard, nextBoard], [previousCardId]) => {
    if (nextCardId === null) {
      void router.replace({ name: 'board' });
      return;
    }

    if (!nextBoard) {
      return;
    }

    if (!nextCard) {
      void router.replace({ name: 'board' });
      return;
    }

    if (previousCardId !== nextCardId || cardDraft.value === null) {
      cardDraft.value = {
        title: nextCard.title,
        description: nextCard.description
      };
    }
  },
  { immediate: true }
);

watch(
  [routeCardId, editingCard, dialogRef],
  ([nextCardId, nextCard]) => {
    if (nextCardId === null || !nextCard) {
      return;
    }

    void syncDialogState();
  },
  { immediate: true, flush: 'post' }
);

onBeforeUnmount(() => {
  const cardId = routeCardId.value;
  if (cardId !== null) {
    stopTypingForCard(cardId);
  }

  const dialog = dialogRef.value;
  if (dialog?.open) {
    dialog.close();
  }
});
</script>
