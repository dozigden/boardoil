<template>
  <ModalDialog :open="editingCard !== null" :title="dialogTitle" size="fill" close-label="Cancel editing" @close="closeCardEditor" @submit="saveCard">
    <template #title>
      <span class="dialog-title-with-pill">
        <template v-if="editingCard">
          <h2 v-if="!isEditingTitle" class="card-dialog-title-heading">
            <button type="button" class="card-dialog-title-button" @click="beginTitleEdit">
              #{{ editingCard.id }} {{ cardDraft?.title ?? editingCard.title }}
            </button>
          </h2>
          <span v-else class="card-dialog-title-edit">
            <span class="card-dialog-title-id">#{{ editingCard.id }}</span>
            <input
              ref="titleEditInput"
              :value="cardDraft?.title ?? editingCard.title"
              maxlength="200"
              aria-label="Card title"
              @focus="announceEditingCardTyping"
              @blur="finishTitleEdit"
              @input="updateEditingCardDraft('title', ($event.target as HTMLInputElement).value)"
              @keydown.enter.prevent="finishTitleEdit"
              @keydown.esc.stop.prevent="cancelTitleEdit"
            />
          </span>
        </template>
        <span v-else>{{ dialogTitle }}</span>
        <span v-if="isEditingCardTyping" class="typing-pill" aria-label="Someone is typing">...</span>
      </span>
    </template>
    <template v-if="editingCard">
      <div class="card-editor-fields">
        <div class="card-editor-tags-row">
          <div class="card-editor-tags-pills" aria-live="polite">
            <Tag
              v-for="tagName in cardDraft?.tagNames ?? editingCard.tagNames"
              :key="tagName"
              :tag-name="tagName"
              class="tag-pill-editable"
            >
              <button
                type="button"
                class="tag-pill-remove"
                :aria-label="`Remove ${tagName}`"
                @click="removeTag(tagName)"
              >
                x
              </button>
            </Tag>
          </div>

          <div class="card-editor-tags-entry">
            <input
              ref="tagEntryInput"
              :value="cardDraft?.tagEntry ?? ''"
              maxlength="320"
              placeholder="add tags"
              aria-label="Add tags"
              @focus="announceEditingCardTyping"
              @blur="stopEditingCardTyping"
              @input="updateTagEntry(($event.target as HTMLInputElement).value)"
              @keydown.enter.prevent="assignTagEntry"
            />
            <button
              type="button"
              class="card-editor-tags-add"
              aria-label="Add tags"
              title="Add tags"
              :disabled="!hasPendingTagEntry"
              @click="assignTagEntry"
            >
              <Check :size="14" aria-hidden="true" />
            </button>
          </div>
        </div>

        <div class="card-editor-description-field">
          <span class="card-editor-field-label">Description</span>
          <MdEditor
            v-model="descriptionDraft"
            aria-label="Card description"
            :max-length="5000"
            min-height="12rem"
            @focus="announceEditingCardTyping"
            @blur="stopEditingCardTyping"
          />
        </div>
      </div>
    </template>
    <template #actions>
      <div v-if="editingCard" class="editor-actions card-modal-actions">
        <button type="button" class="danger card-modal-delete" aria-label="Delete card" title="Delete card" @click="deleteEditingCard">
          <Trash2 :size="16" aria-hidden="true" />
        </button>
        <div class="card-modal-actions-left">
          <button type="submit" class="card-modal-save" aria-label="Save card" title="Save card" :disabled="hasPendingTagEntry">
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
import { computed, nextTick, onBeforeUnmount, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import MdEditor from './MdEditor.vue';
import ModalDialog from './ModalDialog.vue';
import Tag from './Tag.vue';
import { useBoardStore } from '../stores/boardStore';
import { useTagStore } from '../stores/tagStore';
import { mergeTagNames, parseTagInputValues } from '../utils/tagInput';

const route = useRoute();
const router = useRouter();
const boardStore = useBoardStore();
const tagStore = useTagStore();
const { board, typingSummary } = storeToRefs(boardStore);
const { saveCard: saveCardAction, deleteCard, announceTyping, stopTyping } = boardStore;
const { ensureTagsExist } = tagStore;
const cardDraft = ref<{ title: string; description: string; tagNames: string[]; tagEntry: string } | null>(null);
const isEditingTitle = ref(false);
const titleBeforeEdit = ref<string | null>(null);
const titleEditInput = ref<HTMLInputElement | null>(null);
const tagEntryInput = ref<HTMLInputElement | null>(null);

const routeCardId = computed<number | null>(() => {
  const raw = route.params.cardId;
  const parsed = typeof raw === 'string' ? Number.parseInt(raw, 10) : Number.NaN;
  return Number.isFinite(parsed) ? parsed : null;
});

const editingCard = computed(() => boardStore.getCardById(routeCardId.value));
const dialogTitle = computed(() => (editingCard.value ? `Edit Card #${editingCard.value.id}` : 'Edit Card'));
const isEditingCardTyping = computed(() => (routeCardId.value === null ? false : typingSummary.value(routeCardId.value)));
const hasPendingTagEntry = computed(() => (cardDraft.value?.tagEntry.trim().length ?? 0) > 0);
const descriptionDraft = computed({
  get: () => cardDraft.value?.description ?? '',
  set: value => updateEditingCardDraft('description', value)
});

function stopTypingForCard(cardId: number) {
  stopTyping(cardId);
}

function normaliseDescription(value: string) {
  return value.slice(0, 5000);
}

async function closeCardEditor() {
  isEditingTitle.value = false;
  const cardId = routeCardId.value;
  if (cardId !== null) {
    stopTypingForCard(cardId);
  }

  await router.push({ name: 'board' });
}

async function beginTitleEdit() {
  if (!editingCard.value) {
    return;
  }

  titleBeforeEdit.value = cardDraft.value?.title ?? editingCard.value.title;
  isEditingTitle.value = true;
  await nextTick();
  titleEditInput.value?.focus();
  titleEditInput.value?.select();
}

function finishTitleEdit() {
  isEditingTitle.value = false;
  titleBeforeEdit.value = null;
  stopEditingCardTyping();
}

function cancelTitleEdit() {
  if (cardDraft.value && titleBeforeEdit.value !== null) {
    cardDraft.value = {
      ...cardDraft.value,
      title: titleBeforeEdit.value
    };
  }

  finishTitleEdit();
}

function updateEditingCardDraft(field: 'title' | 'description', value: string) {
  if (!cardDraft.value) {
    return;
  }

  const nextValue = field === 'description' ? normaliseDescription(value) : value;
  cardDraft.value = { ...cardDraft.value, [field]: nextValue };
}

function updateTagEntry(value: string) {
  if (!cardDraft.value) {
    return;
  }

  cardDraft.value = {
    ...cardDraft.value,
    tagEntry: value
  };
}

async function assignTagEntry() {
  if (!cardDraft.value) {
    return;
  }

  const parsedTags = parseTagInputValues([cardDraft.value.tagEntry]);
  if (parsedTags.length === 0) {
    return;
  }

  const ensuredTags = await ensureTagsExist(parsedTags);
  if (ensuredTags.length === 0) {
    return;
  }

  cardDraft.value = {
    ...cardDraft.value,
    tagNames: mergeTagNames(cardDraft.value.tagNames, ensuredTags),
    tagEntry: ''
  };

  await nextTick();
  tagEntryInput.value?.focus();
}

function removeTag(tagName: string) {
  if (!cardDraft.value) {
    return;
  }

  cardDraft.value = {
    ...cardDraft.value,
    tagNames: cardDraft.value.tagNames.filter(x => x !== tagName)
  };
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
  if (cardId === null || !cardDraft.value || hasPendingTagEntry.value) {
    return;
  }

  await saveCardAction(cardId, cardDraft.value.title, cardDraft.value.description, cardDraft.value.tagNames);
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

    if (previousCardId !== nextCardId) {
      isEditingTitle.value = false;
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
        description: normaliseDescription(nextCard.description),
        tagNames: [...nextCard.tagNames],
        tagEntry: ''
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

<style scoped>
.card-editor-fields {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  flex: 1;
  min-height: 0;
  overflow: hidden;
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

.card-editor-tags-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
  min-width: 0;
}

.card-editor-tags-pills {
  display: flex;
  align-items: center;
  gap: 0.35rem;
  flex-wrap: wrap;
  flex: 0 1 auto;
  min-width: 0;
}

.card-editor-tags-entry {
  display: inline-flex;
  align-items: stretch;
  align-self: center;
  flex: 0 0 auto;
}

.card-editor-tags-entry input {
  width: 12rem;
  min-width: 10rem;
  margin: 0;
  border-top-right-radius: 0;
  border-bottom-right-radius: 0;
}

.card-editor-tags-add {
  width: 2rem;
  min-width: 2rem;
  margin: 0;
  border-top-left-radius: 0;
  border-bottom-left-radius: 0;
  border-left: none;
  display: inline-flex;
  align-items: center;
  justify-content: center;
}

.card-editor-tags-add:disabled {
  border-color: #d4dbea;
  background: #f3f6fb;
  color: #91a4bf;
  cursor: not-allowed;
}

.card-dialog-title-heading {
  margin: 0;
  font-size: 1.1rem;
  font-weight: 600;
  line-height: 1.2;
}

.card-dialog-title-button {
  width: auto;
  min-width: 0;
  margin: 0;
  border: none;
  border-radius: 0;
  padding: 0;
  background: transparent;
  color: inherit;
  font: inherit;
  text-align: left;
}

.card-dialog-title-button:hover {
  text-decoration: underline;
}

.card-dialog-title-button:focus-visible {
  outline: 2px solid #5b7ca8;
  outline-offset: 2px;
  border-radius: 4px;
}

.card-dialog-title-edit {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
}

.card-dialog-title-id {
  font-weight: 600;
}

.card-dialog-title-edit input {
  width: min(42rem, calc(100vw - 12rem));
  min-width: 14rem;
  margin: 0;
}
</style>
