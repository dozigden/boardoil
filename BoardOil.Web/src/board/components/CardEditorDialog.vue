<template>
  <ModalDialog :open="editingCard !== null" title="Edit Card" size="fill" close-label="Cancel editing" @close="closeCardEditor" @submit="saveCard">
    <template #headerActions>
      <BoDropdown
        v-if="cardDraft"
        class="card-editor-actions-menu"
        align="right"
        label="Card actions"
        :teleport="false"
        :icon="Ellipsis"
        :icon-only="true"
        :icon-size="16"
      >
        <template #default="{ close }">
          <button type="button" class="bo-dropdown-item" @click="close(); void archiveEditingCard()">
            <span class="bo-dropdown-item-main card-editor-menu-item">
              <Archive :size="14" aria-hidden="true" />
              <span>Archive</span>
            </span>
          </button>
          <span class="bo-dropdown-divider" aria-hidden="true"></span>
          <button type="button" class="bo-dropdown-item" @click="close(); void deleteEditingCard()">
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
          :card-id="cardDraftId ?? 0"
          :title="cardDraft.title"
          @update:title="updateDraftTitleFromEditor"
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
              :model-value="cardDraft.description"
              aria-label="Card description"
              :max-length="maxDescriptionLength"
              min-height="12rem"
              :show-toolbar="false"
              @update:model-value="handleDescriptionEditorValueUpdate"
              @focus="handleDescriptionEditorFocus"
              @blur="handleDescriptionEditorBlur"
              @toolbar-state-change="handleDescriptionToolbarStateChange"
              @plain-text-mode-change="handleDescriptionPlainTextModeChange"
            />
          </div>
          <section class="card-editor-comments-section" aria-label="Card comments">
            <div class="card-editor-comment-entry">
              <h3 class="card-editor-comments-title">Comments</h3>
              <div class="card-editor-comment-entry-row">
                <MdEditor
                  ref="commentEditorRef"
                  :model-value="newCommentText"
                  aria-label="Comment"
                  :max-length="maxCommentLength"
                  :min-height="newCommentText.trim().length === 0 ? '3rem' : '6rem'"
                  :show-toolbar="false"
                  @update:model-value="updateCommentDraftFromEditor"
                  @focus="handleCommentEditorFocus"
                  @blur="handleCommentEditorBlur"
                  @toolbar-state-change="handleCommentToolbarStateChange"
                  @plain-text-mode-change="handleCommentPlainTextModeChange"
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
                <aside class="card-editor-comment-author-rail" aria-label="Comment author">
                  <span class="card-editor-comment-author">
                    <UserAvatar
                      :image-relative-path="comment.authorImageRelativePath ?? null"
                      :display-name="comment.authorDisplayName ?? 'Unknown user'"
                      size="sm"
                      class="card-editor-comment-author-avatar"
                    />
                    <span class="card-editor-comment-author-name">{{ comment.authorDisplayName ?? 'Unknown user' }}</span>
                  </span>
                </aside>
                <div class="card-editor-comment-content">
                  <MdViewer
                    class="card-editor-comment-body"
                    :model-value="comment.text"
                    aria-label="Comment content"
                    :max-length="maxCommentLength"
                    min-height="1.5rem"
                  />
                  <time class="card-editor-comment-timestamp" :datetime="comment.createdAtUtc">{{ formatCommentDateTime(comment.createdAtUtc) }}</time>
                </div>
              </article>
            </div>
          </section>
        </div>

        <aside class="card-editor-options" aria-label="Card options">
          <div class="card-editor-option-section">
            <CardTagEditor
              :tag-names="cardDraft.tagNames"
              @update:tag-names="updateDraftTagNamesFromEditor"
              :ensure-tags-exist="tagNames => ensureTagsExist(boardId, tagNames)"
            />
          </div>

          <div class="card-editor-select-field card-editor-column-picker">
            <span class="card-editor-field-label">Column</span>
            <BoDropdown
              class="card-editor-column-dropdown"
              align="left"
              label="Select column"
              :teleport="false"
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
              :teleport="false"
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
                :teleport="false"
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

          <CardSlickPicker
            :slick-name="cardDraft.slickName"
            :slicks="slicks"
            @update:slick-name="updateDraftSlickNameFromEditor"
          />

          <CardExternalUrlEditor
            :external-url="cardDraft.externalUrl"
            @update:external-url="updateDraftExternalUrlFromEditor"
          />

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
import { onBeforeRouteLeave, onBeforeRouteUpdate, useRoute, useRouter } from 'vue-router';
import MdEditor from '../../shared/components/MdEditor.vue';
import MdEditorToolbar from '../../shared/components/MdEditorToolbar.vue';
import MdViewer from '../../shared/components/MdViewer.vue';
import BoDropdown from '../../shared/components/BoDropdown.vue';
import UserAvatar from '../../shared/components/UserAvatar.vue';
import CardTagEditor from './CardTagEditor.vue';
import CardSlickPicker from './CardSlickPicker.vue';
import CardExternalUrlEditor from './CardExternalUrlEditor.vue';
import CardTitleEditor from './CardTitleEditor.vue';
import ModalDialog from '../../shared/components/ModalDialog.vue';
import { useConfirm } from '../../shared/composables/useConfirm';
import { useBoardStore } from '../stores/boardStore';
import { useBoardMembersStore } from '../stores/boardMembersStore';
import { useCardStore } from '../stores/cardStore';
import { useCardTypeStore } from '../stores/cardTypeStore';
import { useCommentStore } from '../stores/commentStore';
import { useSlickStore } from '../stores/slickStore';
import { useTagStore } from '../stores/tagStore';
import { resolveSelectedCardTypeEmoji } from './cardTypeSelection';
import { mdEditorToolbarActions, type MdEditorToolbarActionEvent, type MdEditorToolbarActionId, type MdEditorToolbarActionState } from '../../shared/components/mdEditorToolbarActions';
import { createDisabledToolbarState, resolveActiveIsPlainTextMode, resolveActiveToolbarState } from './cardEditorSharedToolbar';
import type { Card, CardEditModel } from '../../shared/types/boardTypes';

const route = useRoute();
const router = useRouter();
const boardStore = useBoardStore();
const boardMembersStore = useBoardMembersStore();
const cardStore = useCardStore();
const cardTypeStore = useCardTypeStore();
const commentStore = useCommentStore();
const slickStore = useSlickStore();
const tagStore = useTagStore();
const { board, currentBoardId } = storeToRefs(boardStore);
const { members: boardMembers, activeBoardId: boardMembersActiveBoardId } = storeToRefs(boardMembersStore);
const { cardTypes, activeBoardId: cardTypesActiveBoardId, systemCardType } = storeToRefs(cardTypeStore);
const { slicks, activeBoardId: slicksActiveBoardId } = storeToRefs(slickStore);
const { busy: commentsBusy } = storeToRefs(commentStore);
const { saveCard: saveCardAction, deleteCard, archiveCard } = cardStore;
const { loadCardComments, addCardComment: addCardCommentAction } = commentStore;
const { loadMembers } = boardMembersStore;
const { loadCardTypes } = cardTypeStore;
const { loadSlicks } = slickStore;
const { ensureTagsExist } = tagStore;
const { confirm } = useConfirm();
const maxDescriptionLength = 20_000;
const maxCommentLength = 4_000;
const cardDraft = ref<CardEditModel | null>(null);
const cardDraftBoardId = ref<number | null>(null);
const cardDraftId = ref<number | null>(null);
const cardDraftSource = ref<CardEditModel | null>(null);
const isCardDraftDirty = ref(false);
const newCommentText = ref('');
const isCommentDraftDirty = ref(false);
const descriptionEditorRef = ref<InstanceType<typeof MdEditor> | null>(null);
const commentEditorRef = ref<InstanceType<typeof MdEditor> | null>(null);
const activeEditor = ref<'description' | 'comment'>('description');
const descriptionEditorFocused = ref(false);
const commentEditorFocused = ref(false);
const descriptionToolbarState = ref<Partial<Record<MdEditorToolbarActionId, MdEditorToolbarActionState>>>({});
const commentToolbarState = ref<Partial<Record<MdEditorToolbarActionId, MdEditorToolbarActionState>>>({});
const descriptionIsPlainTextMode = ref(false);
const commentIsPlainTextMode = ref(false);

const routeCardId = computed<number | null>(() => {
  const raw = route.params.cardId;
  const parsed = typeof raw === 'string' ? Number.parseInt(raw, 10) : Number.NaN;
  return Number.isFinite(parsed) ? parsed : null;
});

const boardId = computed(() => currentBoardId.value!);

const editingCard = computed(() => cardStore.getCardById(routeCardId.value));
const cardComments = computed(() => commentStore.getCommentsForCard(routeCardId.value));
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

  const cardAssignedUserName = editingCard.value?.assignedUserName;
  return cardAssignedUserName ?? `User #${cardDraft.value.assignedUserId}`;
});
const selectedAssignedMember = computed(() => {
  if (!cardDraft.value || cardDraft.value.assignedUserId === null) {
    return null;
  }

  return boardMembers.value.find(x => x.userId === cardDraft.value!.assignedUserId) ?? null;
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
const hasUnsavedChanges = computed(() => isCardDraftDirty.value || isCommentDraftDirty.value);

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

function handleDescriptionToolbarStateChange(
  state: Partial<Record<MdEditorToolbarActionId, MdEditorToolbarActionState>>
) {
  updateToolbarState('description', state);
}

function handleCommentToolbarStateChange(
  state: Partial<Record<MdEditorToolbarActionId, MdEditorToolbarActionState>>
) {
  updateToolbarState('comment', state);
}

function handleDescriptionPlainTextModeChange(isPlainTextMode: boolean) {
  updatePlainTextMode('description', isPlainTextMode);
}

function handleCommentPlainTextModeChange(isPlainTextMode: boolean) {
  updatePlainTextMode('comment', isPlainTextMode);
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
  clearCardDraft();
  resetCommentDraft();
  activeEditor.value = 'description';
  descriptionEditorFocused.value = false;
  commentEditorFocused.value = false;
}

function resetCommentDraft() {
  newCommentText.value = '';
  isCommentDraftDirty.value = false;
}

async function confirmDiscardUnsavedChanges() {
  if (!hasUnsavedChanges.value) {
    return true;
  }

  return await confirm({
    title: 'Discard unsaved changes',
    message: 'You have unsaved changes in this card. Discard them and leave?',
    confirmLabel: 'Discard',
    danger: true
  });
}

async function closeCardEditor() {
  await closeCardEditorInternal(false);
}

async function closeCardEditorWithoutPrompt() {
  await closeCardEditorInternal(true);
}

async function closeCardEditorInternal(skipUnsavedGuard: boolean) {
  if (!skipUnsavedGuard) {
    const shouldDiscard = await confirmDiscardUnsavedChanges();
    if (!shouldDiscard) {
      return;
    }
  }

  clearDraft();
  await router.push({ name: 'board', params: { boardId: boardId.value } });
}

function updateDraftTitleFromEditor(value: string) {
  if (cardDraft.value?.title === value) {
    return;
  }

  patchFromUser({ title: value });
}

function applyUserDescriptionEdit(value: string) {
  patchFromUser({ description: value });
}

function syncDescriptionFromEditor(value: string) {
  patchFromSystem({ description: value });
}

function handleDescriptionEditorValueUpdate(value: string) {
  if (!cardDraft.value) {
    return;
  }

  const hasChanged = cardDraft.value.description !== value;
  if (!hasChanged) {
    return;
  }

  if (descriptionEditorFocused.value) {
    applyUserDescriptionEdit(value);
    return;
  }

  // Rich editor lifecycle updates can emit model changes without direct typing.
  syncDescriptionFromEditor(value);
}

function updateCommentDraftFromEditor(value: string) {
  if (commentEditorFocused.value && newCommentText.value !== value) {
    isCommentDraftDirty.value = true;
  }
  newCommentText.value = value;
}

function updateDraftTagNamesFromEditor(tagNames: string[]) {
  patchFromUser({
    tagNames: [...tagNames]
  });
}

function updateDraftSlickNameFromEditor(slickName: string | null) {
  patchFromUser({ slickName });
}

function updateDraftExternalUrlFromEditor(externalUrl: string | null) {
  patchFromUser({ externalUrl });
}

function handleDescriptionEditorFocus() {
  descriptionEditorFocused.value = true;
  setActiveEditor('description');
}

function handleDescriptionEditorBlur() {
  descriptionEditorFocused.value = false;
}

function handleCommentEditorFocus() {
  commentEditorFocused.value = true;
  setActiveEditor('comment');
}

function handleCommentEditorBlur() {
  commentEditorFocused.value = false;
}

function setDraftCardTypeId(cardTypeId: number, close?: () => void) {
  patchFromUser({ cardTypeId });
  close?.();
}

function setDraftBoardColumnId(boardColumnId: number, close?: () => void) {
  patchFromUser({ boardColumnId });
  close?.();
}

function setDraftAssignedUserId(assignedUserId: number | null, close?: () => void) {
  patchFromUser({ assignedUserId });
  close?.();
}

function clearCardDraft() {
  cardDraft.value = null;
  cardDraftBoardId.value = null;
  cardDraftId.value = null;
  cardDraftSource.value = null;
  isCardDraftDirty.value = false;
}

function initializeDraftFromCard(sourceBoardId: number, card: Card) {
  if (cardDraftBoardId.value === sourceBoardId && cardDraftId.value === card.id) {
    return false;
  }

  const initialModel = createEditModelFromCard(card);
  cardDraft.value = initialModel;
  cardDraftSource.value = cloneCardEditModel(initialModel);
  cardDraftBoardId.value = sourceBoardId;
  cardDraftId.value = card.id;
  isCardDraftDirty.value = false;
  return true;
}

function patchFromSystem(update: Partial<CardEditModel>) {
  if (!cardDraft.value) {
    return;
  }

  cardDraft.value = {
    ...cardDraft.value,
    ...update
  };
}

function patchFromUser(update: Partial<CardEditModel>) {
  if (!cardDraft.value) {
    return;
  }

  const nextDraft = {
    ...cardDraft.value,
    ...update
  };
  cardDraft.value = nextDraft;

  if (!isCardDraftDirty.value && cardDraftSource.value && !areCardEditModelsEqual(nextDraft, cardDraftSource.value)) {
    isCardDraftDirty.value = true;
  }
}

function redirectToBoard(boardId: number) {
  clearDraft();
  void router.replace({ name: 'board', params: { boardId } });
}

async function ensureEditorLookupsLoaded(boardId: number, isCancelled: () => boolean) {
  if (cardTypesActiveBoardId.value !== boardId) {
    await loadCardTypes(boardId);
    if (isCancelled()) {
      return false;
    }
  }

  if (boardMembersActiveBoardId.value !== boardId) {
    await loadMembers(boardId);
    if (isCancelled()) {
      return false;
    }
  }

  if (slicksActiveBoardId.value !== boardId) {
    await loadSlicks(boardId);
    if (isCancelled()) {
      return false;
    }
  }

  return true;
}

function initializeDraftForCard(nextBoardId: number, nextCard: Card) {
  const refreshedCard = cardStore.getCardById(nextCard.id) ?? nextCard;
  const draftInitialized = initializeDraftFromCard(nextBoardId, refreshedCard);
  if (!draftInitialized) {
    return false;
  }

  resetCommentDraft();
  return true;
}

async function saveCard() {
  const draft = cardDraft.value;
  const cardId = routeCardId.value;
  if (!draft || draft.cardTypeId === null || cardId === null) {
    return;
  }

  const saved = await saveCardAction(cardId, draft);
  if (saved) {
    await closeCardEditorWithoutPrompt();
  }
}

async function addComment() {
  const cardId = routeCardId.value;
  if (!cardDraft.value || commentsBusy.value || cardId === null) {
    return;
  }

  const text = newCommentText.value.trim().slice(0, maxCommentLength);
  if (text.length === 0) {
    return;
  }

  const result = await addCardCommentAction(boardId.value, cardId, text);
  if (!result?.ok) {
    return;
  }

  resetCommentDraft();
}

async function deleteEditingCard() {
  const cardId = routeCardId.value;
  if (!cardDraft.value || cardId === null) {
    return;
  }

  const shouldDelete = await confirm({
    title: 'Delete card',
    message: `Delete card "${cardDraft.value.title}"?`,
    confirmLabel: 'Delete',
    danger: true
  });
  if (!shouldDelete) {
    return;
  }

  const deleted = await deleteCard(cardId);
  if (deleted) {
    await closeCardEditorWithoutPrompt();
  }
}

async function archiveEditingCard() {
  const cardId = routeCardId.value;
  if (!cardDraft.value || cardId === null) {
    return;
  }

  const shouldArchive = await confirm({
    title: 'Archive card',
    message: `Archive card "${cardDraft.value.title}"?`,
    confirmLabel: 'Archive'
  });
  if (!shouldArchive) {
    return;
  }

  const archived = await archiveCard(cardId);
  if (archived) {
    await closeCardEditorWithoutPrompt();
  }
}

watch(
  [boardId, routeCardId, editingCard, board],
  async ([nextBoardId, nextCardId, nextCard, nextBoard], _previous, onCleanup) => {
    let cancelled = false;
    onCleanup(() => {
      cancelled = true;
    });

    if (nextCardId === null) {
      redirectToBoard(nextBoardId);
      return;
    }

    if (!nextBoard) {
      return;
    }

    if (!nextCard) {
      redirectToBoard(nextBoardId);
      return;
    }

    initializeDraftForCard(nextBoardId, nextCard);

    const lookupsLoaded = await ensureEditorLookupsLoaded(nextBoardId, () => cancelled);
    if (!lookupsLoaded) {
      return;
    }

    await loadCardComments(nextBoardId, nextCard.id);
    if (cancelled) {
      return;
    }
  },
  { immediate: true }
);

async function shouldBlockRouteNavigation(
  toName: unknown,
  fromName: unknown,
  toBoardId: unknown,
  fromBoardId: unknown,
  toCardId: unknown,
  fromCardId: unknown
) {
  if (fromName !== 'board-card') {
    return false;
  }

  const navigatingToDifferentRoute = toName !== 'board-card';
  const navigatingToDifferentCard = toBoardId !== fromBoardId || toCardId !== fromCardId;
  if (!navigatingToDifferentRoute && !navigatingToDifferentCard) {
    return false;
  }

  const shouldDiscard = await confirmDiscardUnsavedChanges();
  return !shouldDiscard;
}

onBeforeRouteLeave(async (to, from) => {
  const shouldBlock = await shouldBlockRouteNavigation(
    to.name,
    from.name,
    to.params.boardId,
    from.params.boardId,
    to.params.cardId,
    from.params.cardId
  );
  if (shouldBlock) {
    return false;
  }

  return true;
});

onBeforeRouteUpdate(async (to, from) => {
  const shouldBlock = await shouldBlockRouteNavigation(
    to.name,
    from.name,
    to.params.boardId,
    from.params.boardId,
    to.params.cardId,
    from.params.cardId
  );
  if (shouldBlock) {
    return false;
  }

  return true;
});

function createEditModelFromCard(card: Card): CardEditModel {
  return {
    title: card.title,
    description: card.description,
    externalUrl: card.externalUrl,
    tagNames: [...card.tagNames],
    cardTypeId: card.cardTypeId,
    boardColumnId: card.boardColumnId,
    assignedUserId: card.assignedUserId ?? null,
    slickName: card.slickName ?? null
  };
}

function cloneCardEditModel(model: CardEditModel): CardEditModel {
  return {
    ...model,
    tagNames: [...model.tagNames]
  };
}

function areCardEditModelsEqual(left: CardEditModel, right: CardEditModel) {
  if (left.title !== right.title
    || left.description !== right.description
    || left.externalUrl !== right.externalUrl) {
    return false;
  }

  if (left.cardTypeId !== right.cardTypeId || left.boardColumnId !== right.boardColumnId) {
    return false;
  }

  if (left.assignedUserId !== right.assignedUserId || left.slickName !== right.slickName) {
    return false;
  }

  return areStringArraysEqual(left.tagNames, right.tagNames);
}

function areStringArraysEqual(left: string[], right: string[]) {
  if (left.length !== right.length) {
    return false;
  }

  for (let index = 0; index < left.length; index += 1) {
    if (left[index] !== right[index]) {
      return false;
    }
  }

  return true;
}
</script>

<style scoped>
.dialog-title-with-pill {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  min-width: 0;
  width: 100%;
  max-width: 100%;
  box-sizing: border-box;
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
  display: flex;
  align-items: center;
  gap: 0.35rem;
  width: 100%;
  min-width: 0;
  max-width: min(56rem, calc(100vw - 12rem));
  flex: 1 1 auto;
}

.dialog-title-with-pill :deep(.card-title-edit input) {
  width: 100%;
  flex: 1 1 auto;
  max-width: 100%;
  min-width: 0;
}

@media (max-width: 900px) {
  .dialog-title-with-pill {
    width: 100%;
    max-width: 100%;
  }
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
  display: grid;
  grid-template-columns: 7.5rem minmax(0, 1fr);
  gap: 0.7rem;
  align-items: stretch;
  border: 1px solid var(--bo-border-soft);
  border-radius: 0.4rem;
  padding: 0.5rem 0.6rem;
  background: color-mix(in srgb, var(--bo-bg) 92%, var(--bo-muted-bg) 8%);
  width: 100%;
  box-sizing: border-box;
}

.card-editor-comment-author-rail {
  display: flex;
  align-items: flex-start;
  align-self: stretch;
  min-width: 0;
  padding-right: 0.65rem;
  border-right: 1px solid var(--bo-border-soft);
}

.card-editor-comment-author {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 0.3rem;
  min-width: 0;
}

.card-editor-comment-author-avatar {
  flex-shrink: 0;
}

.card-editor-comment-author-name {
  font-size: 0.82rem;
  font-weight: 600;
  line-height: 1.2;
  overflow-wrap: anywhere;
}

.card-editor-comment-content {
  position: relative;
  align-self: stretch;
  min-width: 0;
  min-height: 100%;
  padding-right: 3.9rem;
}

.card-editor-comment-timestamp {
  position: absolute;
  right: 0;
  bottom: 0;
  font-size: 0.8rem;
  color: var(--bo-muted-text);
  line-height: 1.1;
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

.card-editor-comment-entry :deep(.md-editor-content .tiptap),
.card-editor-comment-entry :deep(.md-editor-textarea),
.card-editor-description-field :deep(.md-editor-content .tiptap),
.card-editor-description-field :deep(.md-editor-textarea) {
  padding-top: 0.3rem;
  padding-bottom: 0.3rem;
}

.card-editor-comment-entry :deep(.md-editor-content .tiptap > :first-child),
.card-editor-description-field :deep(.md-editor-content .tiptap > :first-child) {
  margin-top: 0;
}

.card-editor-comment-entry :deep(.md-editor-content .tiptap > :last-child),
.card-editor-description-field :deep(.md-editor-content .tiptap > :last-child) {
  margin-bottom: 0;
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

@media (max-width: 720px) {
  .card-editor-comment {
    grid-template-columns: 1fr;
    gap: 0.5rem;
  }

  .card-editor-comment-author-rail {
    border-right: none;
    padding-right: 0;
    padding-bottom: 0.35rem;
    border-bottom: 1px solid var(--bo-border-soft);
  }

  .card-editor-comment-author {
    flex-direction: row;
    align-items: center;
    gap: 0.45rem;
  }
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
  width: auto;
  min-width: 11rem;
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
    display: flex;
    flex-direction: column;
    overflow-y: auto;
    overflow-x: hidden;
    padding-right: 0.1rem;
    gap: 0.6rem;
  }

  .card-editor-main {
    flex: 0 0 auto;
    overflow: visible;
    padding-right: 0;
    margin-right: 0;
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
