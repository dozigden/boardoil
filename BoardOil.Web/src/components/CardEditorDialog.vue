<template>
  <ModalDialog :open="editingCard !== null" title="Edit Card" size="fill" close-label="Cancel editing" @close="closeCardEditor" @submit="saveCard">
    <template #title>
      <div class="dialog-title-with-pill">
        <template v-if="selectedCardTypeEmoji">{{ selectedCardTypeEmoji }}</template>
        <CardTitleEditor
          v-if="cardDraft"
          :card-id="cardDraft.id"
          v-model:title="cardDraft.title"
        />
        <span v-else>Edit Card</span>
      </div>
    </template>
    <template v-if="cardDraft">
      <div class="card-editor-fields">
        <label class="card-editor-card-type-field">
          <span class="card-editor-field-label">Type</span>
          <select
            :value="cardDraft.cardTypeId ?? ''"
            @change="setDraftCardTypeId(($event.target as HTMLSelectElement).value)"
          >
            <option
              v-for="cardType in cardTypes"
              :key="cardType.id"
              :value="cardType.id"
            >
              {{ cardType.emoji ? `${cardType.emoji} ${cardType.name}` : cardType.name }}
            </option>
          </select>
        </label>

        <CardTagEditor
          v-model:tag-names="cardDraft.tagNames"
          :ensure-tags-exist="ensureTagsExistForBoard"
        />

        <div class="card-editor-description-field">
          <span class="card-editor-field-label">Description</span>
          <MdEditor
            v-model="descriptionDraft"
            aria-label="Card description"
            :max-length="5000"
            min-height="12rem"
          />
        </div>
      </div>
    </template>
    <template #actions>
      <div v-if="cardDraft" class="editor-actions card-modal-actions">
        <button type="button" class="btn btn--danger" aria-label="Delete card" title="Delete card" @click="deleteEditingCard">
          <Trash2 :size="16" aria-hidden="true" />
        </button>
        <div class="card-modal-actions-left">
          <button type="submit" class="btn" aria-label="Save card" title="Save card">
            <Check :size="16" aria-hidden="true" />
            <span>Save</span>
          </button>
          <button type="button" class="btn btn--secondary" aria-label="Cancel editing" title="Cancel" @click="closeCardEditor">
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
import { computed, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import MdEditor from './MdEditor.vue';
import CardTagEditor from './CardTagEditor.vue';
import CardTitleEditor from './CardTitleEditor.vue';
import ModalDialog from './ModalDialog.vue';
import { useBoardStore } from '../stores/boardStore';
import { useCardTypeStore } from '../stores/cardTypeStore';
import { useTagStore } from '../stores/tagStore';
import { resolveDraftCardTypeId, resolveSelectedCardTypeEmoji } from './cardTypeSelection';

const route = useRoute();
const router = useRouter();
const boardStore = useBoardStore();
const cardTypeStore = useCardTypeStore();
const tagStore = useTagStore();
const { board } = storeToRefs(boardStore);
const { cardTypes, systemCardType } = storeToRefs(cardTypeStore);
const { saveCard: saveCardAction, deleteCard } = boardStore;
const { loadCardTypes } = cardTypeStore;
const { ensureTagsExist } = tagStore;
type CardDraft = { id: number; title: string; description: string; tagNames: string[]; cardTypeId: number | null };

const cardDraft = ref<CardDraft | null>(null);

const routeCardId = computed<number | null>(() => {
  const raw = route.params.cardId;
  const parsed = typeof raw === 'string' ? Number.parseInt(raw, 10) : Number.NaN;
  return Number.isFinite(parsed) ? parsed : null;
});

const routeBoardId = computed<number | null>(() => {
  const raw = route.params.boardId;
  const parsed = typeof raw === 'string' ? Number.parseInt(raw, 10) : Number.NaN;
  return Number.isFinite(parsed) ? parsed : null;
});

const editingCard = computed(() => boardStore.getCardById(routeCardId.value));
const selectedCardTypeEmoji = computed(() => {
  return resolveSelectedCardTypeEmoji(
    cardDraft.value?.cardTypeId ?? null,
    cardTypes.value,
    editingCard.value?.cardTypeEmoji ?? null
  );
});
const descriptionDraft = computed({
  get: () => {
    const draft = cardDraft.value;
    return draft === null ? '' : draft.description;
  },
  set: value => updateEditingCardDraft('description', value)
});

function normaliseDescription(value: string) {
  return value.slice(0, 5000);
}

function clearDraft() {
  cardDraft.value = null;
}

async function closeCardEditor() {
  clearDraft();
  const boardId = routeBoardId.value;
  if (boardId === null) {
    await router.push({ name: 'boards' });
    return;
  }

  await router.push({ name: 'board', params: { boardId } });
}

function updateEditingCardDraft(field: 'title' | 'description', value: string) {
  if (!cardDraft.value) {
    return;
  }

  const nextValue = field === 'description' ? normaliseDescription(value) : value;
  cardDraft.value = { ...cardDraft.value, [field]: nextValue };
}

function setDraftCardTypeId(rawValue: string) {
  if (!cardDraft.value) {
    return;
  }

  const parsed = Number.parseInt(rawValue, 10);
  cardDraft.value = {
    ...cardDraft.value,
    cardTypeId: Number.isFinite(parsed) ? parsed : null
  };
}

async function saveCard() {
  if (!cardDraft.value) {
    return;
  }

  await saveCardAction(
    cardDraft.value.id,
    cardDraft.value.title,
    cardDraft.value.description,
    cardDraft.value.tagNames,
    cardDraft.value.cardTypeId
  );
  await closeCardEditor();
}

async function ensureTagsExistForBoard(tagNames: string[]) {
  return ensureTagsExist(tagNames, routeBoardId.value);
}

async function deleteEditingCard() {
  if (!cardDraft.value) {
    return;
  }

  const shouldDelete = window.confirm(`Delete card "${cardDraft.value.title}"?`);
  if (!shouldDelete) {
    return;
  }

  await deleteCard(cardDraft.value.id);
  await closeCardEditor();
}

watch(
  [routeBoardId, routeCardId, editingCard, board],
  async ([nextBoardId, nextCardId, nextCard, nextBoard]) => {
    if (nextBoardId === null) {
      clearDraft();
      void router.replace({ name: 'boards' });
      return;
    }

    if (nextCardId === null) {
      clearDraft();
      void router.replace({ name: 'board', params: { boardId: nextBoardId } });
      return;
    }

    if (!nextBoard) {
      return;
    }

    if (cardTypes.value.length === 0) {
      await loadCardTypes(nextBoardId);
    }

    if (!nextCard) {
      clearDraft();
      void router.replace({ name: 'board', params: { boardId: nextBoardId } });
      return;
    }

    if (cardDraft.value?.id !== nextCard.id) {
      cardDraft.value = {
        id: nextCard.id,
        title: nextCard.title,
        description: normaliseDescription(nextCard.description),
        tagNames: [...nextCard.tagNames],
        cardTypeId: nextCard.cardTypeId
      };
      return;
    }

    const draftCardTypeExists = cardDraft.value.cardTypeId !== null
      && cardTypes.value.some(x => x.id === cardDraft.value!.cardTypeId);
    if (!draftCardTypeExists) {
      cardDraft.value = {
        ...cardDraft.value,
        cardTypeId: resolveDraftCardTypeId(
          null,
          systemCardType.value?.id ?? null,
          cardTypes.value[0]?.id ?? null
        )
      };
    }
  },
  { immediate: true }
);
</script>

<style scoped>
.dialog-title-with-pill {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
}

.card-editor-fields {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  flex: 1;
  min-height: 0;
  overflow: visible;
}

.card-editor-card-type-field {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
}


.card-editor-description-field {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
  flex: 1 1 0;
  min-height: 0;
  overflow: hidden;
}

.card-editor-field-label {
  font-size: 0.85rem;
}
</style>
