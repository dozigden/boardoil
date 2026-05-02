<template>
  <ModalDialog :open="editingCard !== null" title="Edit Card" size="fill" close-label="Cancel editing" @close="closeCardEditor" @submit="saveCard">
    <template #headerActions>
      <BoDropdown
        v-if="cardDraft"
        class="card-editor-actions-menu"
        align="right"
        label="Card actions"
        :icon="Ellipsis"
        :icon-only="true"
        :icon-size="16"
      >
        <template #default="{ close }">
          <button type="button" class="bo-dropdown-item" @click="archiveEditingCardFromMenu(close)">
            <span class="bo-dropdown-item-main card-editor-menu-item">
              <Archive :size="14" aria-hidden="true" />
              <span>Archive</span>
            </span>
          </button>
          <span class="bo-dropdown-divider" aria-hidden="true"></span>
          <button type="button" class="bo-dropdown-item" @click="deleteEditingCardFromMenu(close)">
            <span class="bo-dropdown-item-main card-editor-menu-item card-editor-menu-item--danger">
              <Trash2 :size="14" aria-hidden="true" />
              <span>Delete</span>
            </span>
          </button>
        </template>
      </BoDropdown>
    </template>
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
          <MdEditorToolbar
            class="card-editor-shared-toolbar"
            :state="activeToolbarState"
            :is-plain-text-mode="activeIsPlainTextMode"
            @action="runSharedToolbarAction"
            @toggle-plain-text-mode="toggleSharedToolbarPlainTextMode"
          />
          <div class="card-editor-description-field">
            <MdEditor
              ref="descriptionEditorRef"
              v-model="descriptionDraft"
              aria-label="Card description"
              :max-length="maxDescriptionLength"
              min-height="12rem"
              :show-toolbar="false"
              @focus="setActiveEditor('description')"
              @toolbar-state-change="updateToolbarState('description', $event)"
              @plain-text-mode-change="updatePlainTextMode('description', $event)"
            />
          </div>
          <section class="card-editor-comments-section" aria-label="Card comments">
            <div class="card-editor-comment-entry">
              <h3 class="card-editor-comments-title">Comments</h3>
              <div class="card-editor-comment-entry-row">
                <MdEditor
                  ref="commentEditorRef"
                  v-model="newCommentText"
                  aria-label="Comment"
                  :max-length="maxCommentLength"
                  min-height="6rem"
                  :show-toolbar="false"
                  @focus="setActiveEditor('comment')"
                  @toolbar-state-change="updateToolbarState('comment', $event)"
                  @plain-text-mode-change="updatePlainTextMode('comment', $event)"
                />
                <button
                  type="button"
                  class="btn card-editor-comment-add-button"
                  :disabled="newCommentText.trim().length === 0 || commentsBusy"
                  @click="addComment"
                >
                  Add
                </button>
              </div>
            </div>

            <div class="card-editor-comments-list">
              <p v-if="cardComments.length === 0" class="card-editor-comments-empty">
                No comments yet.
              </p>
              <article
                v-for="comment in cardComments"
                :key="comment.id"
                class="card-editor-comment"
              >
                <header class="card-editor-comment-header">
                  <span class="card-editor-comment-author">
                    <UserAvatar
                      :image-relative-path="comment.authorImageRelativePath ?? null"
                      :display-name="comment.authorDisplayName ?? 'Unknown user'"
                      size="sm"
                      class="card-editor-comment-author-avatar"
                    />
                    <span class="card-editor-comment-author-name">{{ comment.authorDisplayName ?? 'Unknown user' }}</span>
                  </span>
                  <time class="card-editor-comment-timestamp" :datetime="comment.createdAtUtc">{{ formatCommentDateTime(comment.createdAtUtc) }}</time>
                </header>
                <MdViewer
                  class="card-editor-comment-body"
                  :model-value="comment.text"
                  aria-label="Comment content"
                  :max-length="maxCommentLength"
                  min-height="1.5rem"
                />
              </article>
            </div>
          </section>
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

          <div class="card-editor-select-field card-editor-assigned-user-picker">
            <span class="card-editor-field-label">Assigned to</span>
            <div class="card-editor-assigned-user-control">
              <UserAvatar
                v-if="selectedAssignedMember"
                :image-relative-path="selectedAssignedMember.profileImageRelativePath ?? null"
                :display-name="selectedAssignedMember.displayName"
                size="lg"
                class="card-editor-assignee-avatar card-editor-assignee-avatar--selected"
              />
              <BoDropdown
                align="left"
                label="Select assigned user"
                :text="selectedAssignedUserLabel"
              >
                <template #default="{ close }">
                  <button
                    type="button"
                    class="bo-dropdown-item"
                    @click="setDraftAssignedUserId(null, close)"
                  >
                    <span class="bo-dropdown-item-main">Unassigned</span>
                    <span v-if="cardDraft.assignedUserId === null" class="badge bo-dropdown-item-meta">Selected</span>
                  </button>
                  <button
                    v-for="member in boardMembers"
                    :key="member.userId"
                    type="button"
                    class="bo-dropdown-item"
                    @click="setDraftAssignedUserId(member.userId, close)"
                  >
                    <span class="bo-dropdown-item-main card-editor-assignee-option">
                      <UserAvatar
                        :image-relative-path="member.profileImageRelativePath ?? null"
                        :display-name="member.displayName"
                        size="sm"
                        class="card-editor-assignee-avatar"
                      />
                      <span>{{ member.displayName }}</span>
                    </span>
                    <span v-if="member.userId === cardDraft.assignedUserId" class="badge bo-dropdown-item-meta">Selected</span>
                  </button>
                </template>
              </BoDropdown>
            </div>
          </div>

        </aside>
      </div>
    </template>
    <template #actions>
      <div v-if="cardDraft" class="editor-actions card-modal-actions">
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
import { Archive, Check, Ellipsis, Trash2, X } from 'lucide-vue-next';
import { storeToRefs } from 'pinia';
import { computed, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import MdEditor from '../../shared/components/MdEditor.vue';
import MdEditorToolbar from '../../shared/components/MdEditorToolbar.vue';
import MdViewer from '../../shared/components/MdViewer.vue';
import BoDropdown from '../../shared/components/BoDropdown.vue';
import UserAvatar from '../../shared/components/UserAvatar.vue';
import CardTagEditor from './CardTagEditor.vue';
import CardTitleEditor from './CardTitleEditor.vue';
import ModalDialog from '../../shared/components/ModalDialog.vue';
import { useBoardStore } from '../stores/boardStore';
import { useBoardMembersStore } from '../stores/boardMembersStore';
import { useCardStore } from '../stores/cardStore';
import { useCardTypeStore } from '../stores/cardTypeStore';
import { useCommentStore } from '../stores/commentStore';
import { useTagStore } from '../stores/tagStore';
import { resolveDraftCardTypeId, resolveSelectedCardTypeEmoji } from './cardTypeSelection';
import { mdEditorToolbarActions, type MdEditorToolbarActionEvent, type MdEditorToolbarActionId, type MdEditorToolbarActionState } from '../../shared/components/mdEditorToolbarActions';
import { createDisabledToolbarState, resolveActiveIsPlainTextMode, resolveActiveToolbarState } from './cardEditorSharedToolbar';

const route = useRoute();
const router = useRouter();
const boardStore = useBoardStore();
const boardMembersStore = useBoardMembersStore();
const cardStore = useCardStore();
const cardTypeStore = useCardTypeStore();
const commentStore = useCommentStore();
const tagStore = useTagStore();
const { board } = storeToRefs(boardStore);
const { members: boardMembers, activeBoardId: boardMembersActiveBoardId } = storeToRefs(boardMembersStore);
const { cardTypes, systemCardType } = storeToRefs(cardTypeStore);
const { busy: commentsBusy } = storeToRefs(commentStore);
const { saveCard: saveCardAction, deleteCard, archiveCard } = cardStore;
const { loadCardComments, addCardComment: addCardCommentAction } = commentStore;
const { loadMembers } = boardMembersStore;
const { loadCardTypes } = cardTypeStore;
const { ensureTagsExist } = tagStore;
const maxDescriptionLength = 20_000;
const maxCommentLength = 4_000;
type CardDraft = {
  id: number;
  title: string;
  description: string;
  tagNames: string[];
  cardTypeId: number | null;
  boardColumnId: number;
  assignedUserId: number | null;
  assignedUserName: string | null;
};

const cardDraft = ref<CardDraft | null>(null);
const newCommentText = ref('');
const descriptionEditorRef = ref<InstanceType<typeof MdEditor> | null>(null);
const commentEditorRef = ref<InstanceType<typeof MdEditor> | null>(null);
const activeEditor = ref<'description' | 'comment'>('description');
const descriptionToolbarState = ref<Partial<Record<MdEditorToolbarActionId, MdEditorToolbarActionState>>>({});
const commentToolbarState = ref<Partial<Record<MdEditorToolbarActionId, MdEditorToolbarActionState>>>({});
const descriptionIsPlainTextMode = ref(false);
const commentIsPlainTextMode = ref(false);

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
const cardComments = computed(() => commentStore.getCommentsForCard(cardDraft.value?.id ?? null));
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
const selectedAssignedUserLabel = computed(() => {
  if (!cardDraft.value || cardDraft.value.assignedUserId === null) {
    return 'Unassigned';
  }

  const selectedMember = boardMembers.value.find(x => x.userId === cardDraft.value!.assignedUserId);
  if (selectedMember) {
    return selectedMember.displayName;
  }

  return cardDraft.value.assignedUserName ?? `User #${cardDraft.value.assignedUserId}`;
});
const selectedAssignedMember = computed(() => {
  if (!cardDraft.value || cardDraft.value.assignedUserId === null) {
    return null;
  }

  return boardMembers.value.find(x => x.userId === cardDraft.value!.assignedUserId) ?? null;
});
const descriptionDraft = computed({
  get: () => {
    const draft = cardDraft.value;
    return draft === null ? '' : draft.description;
  },
  set: value => updateEditingCardDraft('description', value)
});
const disabledToolbarState = computed<Partial<Record<MdEditorToolbarActionId, MdEditorToolbarActionState>>>(() => {
  return createDisabledToolbarState(mdEditorToolbarActions.map(action => action.id));
});
const activeToolbarState = computed(() => {
  return resolveActiveToolbarState(
    activeEditor.value,
    descriptionToolbarState.value,
    commentToolbarState.value,
    disabledToolbarState.value
  );
});
const activeIsPlainTextMode = computed(() => {
  return resolveActiveIsPlainTextMode(
    activeEditor.value,
    descriptionIsPlainTextMode.value,
    commentIsPlainTextMode.value
  );
});

function setActiveEditor(editor: 'description' | 'comment') {
  activeEditor.value = editor;
}

function updateToolbarState(
  editor: 'description' | 'comment',
  state: Partial<Record<MdEditorToolbarActionId, MdEditorToolbarActionState>>
) {
  if (editor === 'comment') {
    commentToolbarState.value = state;
    return;
  }

  descriptionToolbarState.value = state;
}

function updatePlainTextMode(editor: 'description' | 'comment', isPlainTextMode: boolean) {
  if (editor === 'comment') {
    commentIsPlainTextMode.value = isPlainTextMode;
    return;
  }

  descriptionIsPlainTextMode.value = isPlainTextMode;
}

function runSharedToolbarAction(actionEvent: MdEditorToolbarActionEvent) {
  const editor = activeEditor.value === 'comment'
    ? commentEditorRef.value
    : descriptionEditorRef.value;
  editor?.runToolbarAction(actionEvent);
}

function toggleSharedToolbarPlainTextMode() {
  const editor = activeEditor.value === 'comment'
    ? commentEditorRef.value
    : descriptionEditorRef.value;
  editor?.togglePlainTextMode();
}

function normaliseDescription(value: string) {
  return value.slice(0, maxDescriptionLength);
}

function formatCommentDateTime(value: string) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short'
  }).format(date);
}

function clearDraft() {
  cardDraft.value = null;
  newCommentText.value = '';
  activeEditor.value = 'description';
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

function setDraftAssignedUserId(assignedUserId: number | null, close?: () => void) {
  if (!cardDraft.value) {
    return;
  }

  const assignedUserName = assignedUserId === null
    ? null
    : (boardMembers.value.find(x => x.userId === assignedUserId)?.userName ?? cardDraft.value.assignedUserName);
  cardDraft.value = {
    ...cardDraft.value,
    assignedUserId,
    assignedUserName
  };
  close?.();
}

async function saveCard() {
  if (!cardDraft.value || cardDraft.value.cardTypeId === null) {
    return;
  }

  const saved = await saveCardAction(
    cardDraft.value.id,
    cardDraft.value.title,
    cardDraft.value.description,
    cardDraft.value.tagNames,
    cardDraft.value.cardTypeId,
    cardDraft.value.boardColumnId,
    cardDraft.value.assignedUserId
  );
  if (saved) {
    await closeCardEditor();
  }
}

async function ensureTagsExistForBoard(tagNames: string[]) {
  return ensureTagsExist(tagNames, routeBoardId.value);
}

async function addComment() {
  if (!cardDraft.value || commentsBusy.value) {
    return;
  }

  const text = newCommentText.value.trim().slice(0, maxCommentLength);
  if (text.length === 0) {
    return;
  }

  const boardId = routeBoardId.value;
  if (boardId === null) {
    return;
  }

  const result = await addCardCommentAction(boardId, cardDraft.value.id, text);
  if (!result?.ok) {
    return;
  }

  newCommentText.value = '';
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

function deleteEditingCardFromMenu(close: () => void) {
  close();
  void deleteEditingCard();
}

function archiveEditingCardFromMenu(close: () => void) {
  close();
  void archiveEditingCard();
}

watch(
  [routeBoardId, routeCardId, editingCard, board],
  async ([nextBoardId, nextCardId, nextCard, nextBoard], _previous, onCleanup) => {
    let cancelled = false;
    onCleanup(() => {
      cancelled = true;
    });

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
      if (cancelled) {
        return;
      }
    }
    if (boardMembersActiveBoardId.value !== nextBoardId || boardMembers.value.length === 0) {
      await loadMembers(nextBoardId);
      if (cancelled) {
        return;
      }
    }

    if (!nextCard) {
      clearDraft();
      void router.replace({ name: 'board', params: { boardId: nextBoardId } });
      return;
    }

    await loadCardComments(nextBoardId, nextCard.id);
    if (cancelled) {
      return;
    }

    if (cardDraft.value?.id !== nextCard.id) {
      const refreshedCard = cardStore.getCardById(nextCard.id) ?? nextCard;
      cardDraft.value = {
        id: refreshedCard.id,
        title: refreshedCard.title,
        description: normaliseDescription(refreshedCard.description),
        tagNames: [...refreshedCard.tagNames],
        cardTypeId: refreshedCard.cardTypeId,
        boardColumnId: refreshedCard.boardColumnId,
        assignedUserId: refreshedCard.assignedUserId ?? null,
        assignedUserName: refreshedCard.assignedUserName ?? null
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

    const finalDraft = cardDraft.value;
    if (!finalDraft) {
      return;
    }

    if (finalDraft.assignedUserId !== null) {
      const selectedMember = boardMembers.value.find(x => x.userId === finalDraft.assignedUserId);
      if (selectedMember && selectedMember.userName !== finalDraft.assignedUserName) {
        cardDraft.value = {
          ...finalDraft,
          assignedUserName: selectedMember.userName
        };
      }
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
  min-width: 0;
  flex: 1 1 auto;
  padding-right: 5.4rem;
}

.dialog-title-with-pill :deep(.card-title-editor) {
  min-width: 0;
  flex: 1 1 auto;
}

.dialog-title-with-pill :deep(.card-title-button) {
  display: block;
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.dialog-title-with-pill :deep(.card-title-edit) {
  min-width: 0;
  max-width: 100%;
}

.dialog-title-with-pill :deep(.card-title-edit input) {
  width: 100%;
  min-width: 0;
}

.card-editor-layout {
  display: grid;
  grid-template-columns: minmax(0, 3fr) minmax(14rem, 1fr);
  gap: 0;
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
  overflow-y: auto;
  overflow-x: hidden;
  padding-right: 0.75rem;
  margin-right: 0.25rem;
}

.card-editor-shared-toolbar {
  position: sticky;
  top: 0;
  z-index: 2;
  padding: 0.2rem 0 0.35rem;
  background: var(--bo-surface-base);
}

.card-editor-comments-section {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  border-top: 1px solid var(--bo-border-soft);
  padding-top: 0.5rem;
  width: 100%;
}

.card-editor-comments-title {
  margin: 0;
  font-size: 0.95rem;
}

.card-editor-comments-list {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
  width: 100%;
}

.card-editor-comments-empty {
  margin: 0;
  color: var(--bo-muted-text);
  font-size: 0.9rem;
}

.card-editor-comment {
  border: 1px solid var(--bo-border-soft);
  border-radius: 0.4rem;
  padding: 0.5rem 0.6rem;
  background: color-mix(in srgb, var(--bo-bg) 92%, var(--bo-muted-bg) 8%);
  width: 100%;
  box-sizing: border-box;
}

.card-editor-comment-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.35rem;
}

.card-editor-comment-author {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  min-width: 0;
}

.card-editor-comment-author-avatar {
  flex-shrink: 0;
}

.card-editor-comment-author-name {
  font-size: 0.88rem;
  font-weight: 600;
}

.card-editor-comment-timestamp {
  font-size: 0.8rem;
  color: var(--bo-muted-text);
  white-space: nowrap;
}

.card-editor-comment-entry {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
  width: 100%;
}

.card-editor-comment-entry-row {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  gap: 0.5rem;
  align-items: end;
}

.card-editor-comment-add-button {
  align-self: end;
}

.card-editor-comment-entry :deep(.md-editor) {
  flex: 0 0 auto;
  min-height: fit-content;
  width: 100%;
}

.card-editor-comment-entry :deep(.md-editor-input),
.card-editor-comment-entry :deep(.md-editor-content) {
  flex: 0 0 auto;
  min-height: fit-content;
  overflow: visible;
  width: 100%;
}

.card-editor-comment-entry :deep(.md-editor-content .tiptap),
.card-editor-comment-entry :deep(.md-editor-textarea) {
  height: auto;
  max-height: none;
  overflow-y: visible;
  width: 100%;
}

.card-editor-comment-body :deep(.md-viewer) {
  flex: 0 0 auto;
  min-height: fit-content;
  overflow: visible;
}

.card-editor-comment-body :deep(.md-viewer-content) {
  overflow: visible;
}

.card-editor-comment-body :deep(.md-viewer-content .tiptap) {
  height: auto;
  max-height: none;
  min-height: 1.5rem;
  margin: 0;
  padding: 0;
  border: none;
  border-radius: 0;
  background: transparent;
  overflow-y: visible;
}

.card-editor-select-field {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.card-editor-column-picker :deep(.bo-dropdown),
.card-editor-type-picker :deep(.bo-dropdown),
.card-editor-assigned-user-picker :deep(.bo-dropdown) {
  width: 100%;
}

.card-editor-column-picker :deep(.bo-dropdown-trigger),
.card-editor-type-picker :deep(.bo-dropdown-trigger),
.card-editor-assigned-user-picker :deep(.bo-dropdown-trigger) {
  width: 100%;
  justify-content: space-between;
}

.card-editor-column-picker :deep(.bo-dropdown-panel),
.card-editor-type-picker :deep(.bo-dropdown-panel),
.card-editor-assigned-user-picker :deep(.bo-dropdown-panel) {
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
  flex: 0 0 auto;
  min-height: fit-content;
  overflow: visible;
  width: 100%;
}

.card-editor-description-field :deep(.md-editor),
.card-editor-description-field :deep(.md-editor-input),
.card-editor-description-field :deep(.md-editor-content) {
  flex: 0 0 auto;
  min-height: fit-content;
  overflow: visible;
  width: 100%;
}

.card-editor-description-field :deep(.md-editor-content .tiptap) {
  height: auto;
  max-height: none;
  overflow-y: visible;
  width: 100%;
}

.card-editor-description-field :deep(.md-editor-textarea) {
  max-height: none;
  overflow-y: hidden;
}

.card-editor-field-label {
  font-size: 0.85rem;
}

.card-editor-actions-menu :deep(.bo-dropdown-trigger) {
  width: 2.2rem;
  justify-content: center;
}

.card-editor-actions-menu {
  flex: 0 0 auto;
}

.card-editor-menu-item {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
}

.card-editor-menu-item--danger {
  color: var(--bo-danger);
}

.card-editor-assignee-option {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
}

.card-editor-assigned-user-control {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.card-editor-assignee-avatar {
  flex-shrink: 0;
}

.card-editor-assignee-avatar--selected {
  flex-shrink: 0;
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
    padding-right: 5.1rem;
  }

  .card-editor-layout {
    gap: 0.6rem;
  }

  .card-editor-options {
    display: flex;
    flex-direction: column;
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
