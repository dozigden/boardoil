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
      <div class="card-editor-layout">
        <div class="card-editor-main">
          <div class="card-editor-description-field">
            <MdEditor
              v-model="descriptionDraft"
              aria-label="Card description"
              :max-length="maxDescriptionLength"
              min-height="12rem"
            />
          </div>
        </div>

        <aside class="card-editor-options" aria-label="Card options">
          <div class="card-editor-option-section">
            <CardTagEditor
              v-model:tag-names="cardDraft.tagNames"
              :ensure-tags-exist="ensureTagsExistForBoard"
            />
          </div>

          <div class="card-editor-select-field card-editor-column-picker">
            <span class="card-editor-field-label">Column</span>
            <BoDropdown
              class="card-editor-column-dropdown"
              align="left"
              label="Select column"
              :text="selectedBoardColumnLabel"
            >
              <template #default="{ close }">
                <button
                  v-for="column in boardColumns"
                  :key="column.id"
                  type="button"
                  class="bo-dropdown-item"
                  @click="setDraftBoardColumnId(column.id, close)"
                >
                  <span class="bo-dropdown-item-main">{{ column.title }}</span>
                  <span v-if="column.id === cardDraft.boardColumnId" class="badge bo-dropdown-item-meta">Selected</span>
                </button>
              </template>
            </BoDropdown>
          </div>

          <div class="card-editor-select-field card-editor-type-picker">
            <span class="card-editor-field-label">Type</span>
            <BoDropdown
              align="left"
              label="Select card type"
              :text="selectedCardTypeLabel"
            >
              <template #default="{ close }">
                <button
                  v-for="cardType in cardTypes"
                  :key="cardType.id"
                  type="button"
                  class="bo-dropdown-item"
                  @click="setDraftCardTypeId(cardType.id, close)"
                >
                  <span class="bo-dropdown-item-main">
                    {{ cardType.emoji ? `${cardType.emoji} ${cardType.name}` : cardType.name }}
                  </span>
                  <span v-if="cardType.id === cardDraft.cardTypeId" class="badge bo-dropdown-item-meta">Selected</span>
                </button>
              </template>
            </BoDropdown>
          </div>

        </aside>
      </div>
    </template>
    <template #actions>
      <div v-if="cardDraft" class="editor-actions card-modal-actions">
        <div class="card-modal-actions-left">
          <button type="button" class="btn btn--secondary" aria-label="Archive card" title="Archive card" @click="archiveEditingCard">
            <Archive :size="16" aria-hidden="true" />
            <span>Archive</span>
          </button>
          <button type="button" class="btn btn--danger" aria-label="Delete card" title="Delete card" @click="deleteEditingCard">
            <Trash2 :size="16" aria-hidden="true" />
            <span>Delete</span>
          </button>
        </div>
        <div class="card-modal-actions-right">
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
import { Archive, Check, Trash2, X } from 'lucide-vue-next';
import { storeToRefs } from 'pinia';
import { computed, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import MdEditor from '../../shared/components/MdEditor.vue';
import BoDropdown from '../../shared/components/BoDropdown.vue';
import CardTagEditor from './CardTagEditor.vue';
import CardTitleEditor from './CardTitleEditor.vue';
import ModalDialog from '../../shared/components/ModalDialog.vue';
import { useBoardStore } from '../stores/boardStore';
import { useCardStore } from '../stores/cardStore';
import { useCardTypeStore } from '../stores/cardTypeStore';
import { useTagStore } from '../stores/tagStore';
import { resolveDraftCardTypeId, resolveSelectedCardTypeEmoji } from './cardTypeSelection';

const route = useRoute();
const router = useRouter();
const boardStore = useBoardStore();
const cardStore = useCardStore();
const cardTypeStore = useCardTypeStore();
const tagStore = useTagStore();
const { board } = storeToRefs(boardStore);
const { cardTypes, systemCardType } = storeToRefs(cardTypeStore);
const { saveCard: saveCardAction, deleteCard, archiveCard } = cardStore;
const { loadCardTypes } = cardTypeStore;
const { ensureTagsExist } = tagStore;
const maxDescriptionLength = 20_000;
type CardDraft = { id: number; title: string; description: string; tagNames: string[]; cardTypeId: number | null; boardColumnId: number };

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

const editingCard = computed(() => cardStore.getCardById(routeCardId.value));
const boardColumns = computed(() => board.value?.columns ?? []);
const selectedBoardColumnLabel = computed(() => {
  if (!cardDraft.value) {
    return 'Select column';
  }

  return boardColumns.value.find(column => column.id === cardDraft.value!.boardColumnId)?.title ?? 'Select column';
});
const selectedCardTypeLabel = computed(() => {
  if (!cardDraft.value) {
    return 'Select card type';
  }

  const selectedCardType = cardTypes.value.find(cardType => cardType.id === cardDraft.value!.cardTypeId);
  if (!selectedCardType) {
    return 'Select card type';
  }

  return selectedCardType.emoji
    ? `${selectedCardType.emoji} ${selectedCardType.name}`
    : selectedCardType.name;
});
const selectedCardTypeEmoji = computed(() => {
  return resolveSelectedCardTypeEmoji(
    cardDraft.value?.cardTypeId ?? null,
    cardTypes.value
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
  return value.slice(0, maxDescriptionLength);
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

function setDraftCardTypeId(cardTypeId: number, close?: () => void) {
  if (!cardDraft.value) {
    return;
  }

  cardDraft.value = {
    ...cardDraft.value,
    cardTypeId
  };
  close?.();
}

function setDraftBoardColumnId(boardColumnId: number, close?: () => void) {
  if (!cardDraft.value) {
    return;
  }

  cardDraft.value = {
    ...cardDraft.value,
    boardColumnId
  };
  close?.();
}

async function saveCard() {
  if (!cardDraft.value || cardDraft.value.cardTypeId === null) {
    return;
  }

  await saveCardAction(
    cardDraft.value.id,
    cardDraft.value.title,
    cardDraft.value.description,
    cardDraft.value.tagNames,
    cardDraft.value.cardTypeId,
    cardDraft.value.boardColumnId
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

  const deleted = await deleteCard(cardDraft.value.id);
  if (deleted) {
    await closeCardEditor();
  }
}

async function archiveEditingCard() {
  if (!cardDraft.value) {
    return;
  }

  const shouldArchive = window.confirm(`Archive card "${cardDraft.value.title}"?`);
  if (!shouldArchive) {
    return;
  }

  const archived = await archiveCard(cardDraft.value.id);
  if (archived) {
    await closeCardEditor();
  }
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
        cardTypeId: nextCard.cardTypeId,
        boardColumnId: nextCard.boardColumnId
      };
      return;
    }

    const draft = cardDraft.value;
    if (!draft) {
      return;
    }

    const draftColumnExists = nextBoard.columns.some(x => x.id === draft.boardColumnId);
    if (!draftColumnExists) {
      cardDraft.value = {
        ...draft,
        boardColumnId: nextCard.boardColumnId
      };
    }

    const nextDraft = cardDraft.value;
    if (!nextDraft) {
      return;
    }

    const draftCardTypeExists = nextDraft.cardTypeId !== null
      && cardTypes.value.some(x => x.id === nextDraft.cardTypeId);
    if (!draftCardTypeExists) {
      cardDraft.value = {
        ...nextDraft,
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

.card-editor-layout {
  display: grid;
  grid-template-columns: minmax(0, 3fr) minmax(14rem, 1fr);
  gap: 0.85rem;
  flex: 1;
  min-height: 0;
  overflow: hidden;
}

.card-editor-main {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  min-width: 0;
  min-height: 0;
  overflow: hidden;
}

.card-editor-select-field {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.card-editor-column-picker :deep(.bo-dropdown),
.card-editor-type-picker :deep(.bo-dropdown) {
  width: 100%;
}

.card-editor-column-picker :deep(.bo-dropdown-trigger),
.card-editor-type-picker :deep(.bo-dropdown-trigger) {
  width: 100%;
  justify-content: space-between;
}

.card-editor-column-picker :deep(.bo-dropdown-panel),
.card-editor-type-picker :deep(.bo-dropdown-panel) {
  width: 100%;
  min-width: 0;
}

.card-editor-option-section {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  min-width: 0;
}

.card-editor-options :deep(.card-tag-editor-pills) {
  width: 100%;
}

.card-editor-options :deep(.card-tag-editor-entry) {
  display: flex;
  width: 100%;
}

.card-editor-options :deep(.card-tag-editor-entry input) {
  width: 100%;
  min-width: 0;
}

.card-editor-options {
  display: flex;
  flex-direction: column;
  gap: 0.65rem;
  min-width: 0;
  min-height: 0;
  border-left: 1px solid var(--bo-border-soft);
  padding-left: 0.85rem;
}

.card-editor-description-field {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  flex: 1 1 0;
  min-height: 0;
  overflow: hidden;
}

.card-editor-field-label {
  font-size: 0.85rem;
}

.card-modal-actions-right {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
}

@media (max-width: 900px) {
  .card-editor-layout {
    grid-template-columns: minmax(0, 1fr);
    grid-template-rows: minmax(0, 1fr) auto;
  }

  .card-editor-options {
    border-left: none;
    border-top: 1px solid var(--bo-border-soft);
    padding-left: 0;
    padding-top: 0.75rem;
  }
}

@media (max-width: 720px) {
  .dialog-title-with-pill {
    gap: 0.35rem;
    min-width: 0;
    width: calc(100% - 2.1rem);
  }

  .card-editor-layout {
    gap: 0.6rem;
  }

  .card-editor-options {
    display: grid;
    grid-template-columns: minmax(0, 1fr) minmax(7rem, auto);
    align-items: end;
    gap: 0.5rem;
    padding-top: 0.6rem;
  }

  .card-editor-option-section {
    min-width: 0;
  }

  .card-editor-select-field {
    min-width: 0;
  }

  .card-editor-options :deep(.card-tag-editor-entry input) {
    width: 100%;
    min-width: 0;
  }

  .card-editor-description-field :deep(.md-editor) {
    --md-editor-min-height: 8rem;
    height: 100%;
  }

  .card-editor-description-field :deep(.md-editor-input),
  .card-editor-description-field :deep(.md-editor-content) {
    height: 100%;
  }

  :deep(.card-modal-content) {
    padding: 0.75rem;
  }
}
</style>
