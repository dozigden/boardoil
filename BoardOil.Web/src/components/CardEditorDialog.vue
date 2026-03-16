<template>
  <ModalDialog :open="editingCard !== null" :title="dialogTitle" close-label="Cancel editing" @close="closeCardEditor" @submit="saveCard">
    <template #title>
      <span class="dialog-title-with-pill">
        <span>{{ dialogTitle }}</span>
        <span v-if="isEditingCardTyping" class="typing-pill" aria-label="Someone is typing">...</span>
      </span>
    </template>
    <template v-if="editingCard">
      <label>
        Title
        <input
          :value="cardDraft?.title ?? editingCard.title"
          maxlength="200"
          @focus="announceEditingCardTyping"
          @blur="stopEditingCardTyping"
          @input="updateEditingCardDraft('title', ($event.target as HTMLInputElement).value)"
        />
      </label>

      <label>
        Description
        <textarea
          :value="cardDraft?.description ?? editingCard.description"
          maxlength="5000"
          @focus="announceEditingCardTyping"
          @blur="stopEditingCardTyping"
          @input="updateEditingCardDraft('description', ($event.target as HTMLTextAreaElement).value)"
        />
      </label>
    </template>
    <template #actions>
      <div v-if="editingCard" class="editor-actions card-modal-actions">
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
    </template>
  </ModalDialog>
</template>

<script setup lang="ts">
import { Check, Trash2, X } from 'lucide-vue-next';
import { storeToRefs } from 'pinia';
import { computed, onBeforeUnmount, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import ModalDialog from './ModalDialog.vue';
import { useBoardStore } from '../stores/boardStore';

const route = useRoute();
const router = useRouter();
const boardStore = useBoardStore();
const { board, typingSummary } = storeToRefs(boardStore);
const { saveCard: saveCardAction, deleteCard, announceTyping, stopTyping } = boardStore;
const cardDraft = ref<{ title: string; description: string } | null>(null);

const routeCardId = computed<number | null>(() => {
  const raw = route.params.cardId;
  const parsed = typeof raw === 'string' ? Number.parseInt(raw, 10) : Number.NaN;
  return Number.isFinite(parsed) ? parsed : null;
});

const editingCard = computed(() => boardStore.getCardById(routeCardId.value));
const dialogTitle = computed(() => (editingCard.value ? `Edit Card #${editingCard.value.id}` : 'Edit Card'));
const isEditingCardTyping = computed(() => (routeCardId.value === null ? false : typingSummary.value(routeCardId.value)));

function stopTypingForCard(cardId: number) {
  stopTyping(cardId);
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

function announceEditingCardTyping() {
  const cardId = routeCardId.value;
  if (cardId === null) {
    return;
  }

  announceTyping(cardId);
}

function stopEditingCardTyping() {
  const cardId = routeCardId.value;
  if (cardId === null) {
    return;
  }

  stopTyping(cardId);
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

onBeforeUnmount(() => {
  const cardId = routeCardId.value;
  if (cardId !== null) {
    stopTypingForCard(cardId);
  }
});
</script>
