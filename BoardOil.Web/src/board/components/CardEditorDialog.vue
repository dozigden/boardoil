<template>
  <FixedChromeDialog
    ref="editorDialog"
    :busy="savingCard"
    class="card-editor-dialog"
    :open="editingCard !== null"
    :title="isDuplicatingCard ? 'Duplicate Card' : 'Edit Card'"
    size="fill"
    body-mode="managed"
    :close-label="isDuplicatingCard ? 'Cancel duplicate' : 'Cancel editing'"
    @close="closeCardEditor"
    @submit="saveCard"
    @keydown="handleCardEditorKeydown"
  >
    <template #headerActions>
      <BoDropdown
        v-if="cardDraft && !isDuplicatingCard"
        class="card-editor-actions-menu"
        align="right"
        label="Card actions"
        :teleport="false"
        :icon="Ellipsis"
        :icon-only="true"
        :icon-size="16"
      >
        <template #default="{ close }">
          <button
            type="button"
            class="bo-dropdown-item"
            :disabled="isDuplicateDisabled"
            :title="duplicateActionTitle"
            @click="close(); beginDuplicate()"
          >
            <span class="bo-dropdown-item-main card-editor-menu-item">
              <Copy :size="14" aria-hidden="true" />
              <span>Duplicate</span>
            </span>
          </button>
          <button
            type="button"
            class="bo-dropdown-item"
            :disabled="isTransferDisabled"
            :title="transferActionTitle"
            @click="close(); void openCardTransfer()"
          >
            <span class="bo-dropdown-item-main card-editor-menu-item">
              <ArrowRightLeft :size="14" aria-hidden="true" />
              <span>Move to another board</span>
            </span>
          </button>
          <button type="button" class="bo-dropdown-item" :disabled="attachments.busy" @click="close(); void archiveEditingCard()">
            <span class="bo-dropdown-item-main card-editor-menu-item">
              <Archive :size="14" aria-hidden="true" />
              <span>Archive</span>
            </span>
          </button>
          <span class="bo-dropdown-divider" aria-hidden="true"></span>
          <button type="button" class="bo-dropdown-item" :disabled="attachments.busy" @click="close(); void deleteEditingCard()">
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
        <span v-if="selectedCardTypeEmoji" class="bo-emoji" aria-hidden="true">{{ selectedCardTypeEmoji }}</span>
        <span v-if="isDuplicatingCard" class="badge">Duplicate</span>
        <CardTitleEditor
          v-if="cardDraft"
          :card-id="isDuplicatingCard ? null : cardDraftId"
          :title="cardDraft.title"
          @update:title="updateDraftTitleFromEditor"
        />
        <span v-else>Edit Card</span>
      </div>
    </template>
    <template v-if="cardDraft">
      <div v-if="cardSaveError && (Object.keys(cardSaveError.validationErrors ?? {}).length === 0 || Object.keys(cardSummaryErrors).length)" ref="cardErrorSummary" class="card-save-errors" role="alert" tabindex="-1">
        <p v-if="Object.keys(cardSaveError.validationErrors ?? {}).length === 0"><CircleAlert :size="14" aria-hidden="true" />{{ cardSaveError.message }}</p>
        <p v-for="(messages, path) in cardSummaryErrors" :key="path"><CircleAlert :size="14" aria-hidden="true" />{{ messages.join(' ') }}</p>
      </div>
      <div class="card-editor-layout">
        <div class="card-editor-main">
          <MdEditorToolbar
            class="card-editor-shared-toolbar"
            :state="activeToolbarState"
            :is-plain-text-mode="activeIsPlainTextMode"
            :show-image-action="!isDuplicatingCard && attachments.mutable"
            :image-action-disabled="attachments.busy || attachments.loading || attachments.maxUploadByteLength === 0"
            @action="runSharedToolbarAction"
            @image="selectActiveEditorImage"
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
              :image-context="cardImageContext"
              :image-remove="removeDescriptionImage"
              :image-upload="cardImageUpload"
              :image-upload-cancel="attachments.cancel"
              @update:model-value="handleDescriptionEditorValueUpdate"
              @user-edit="applyUserDescriptionEdit"
              @focus="handleDescriptionEditorFocus"
              @blur="handleDescriptionEditorBlur"
              @escape="closeCardEditor"
              @toolbar-state-change="handleDescriptionToolbarStateChange"
              @plain-text-mode-change="handleDescriptionPlainTextModeChange"
              @image-upload-blocking-change="handleDescriptionImageBlockingChange"
            />
          </div>
          <section v-if="!isDuplicatingCard" class="card-editor-comments-section" aria-label="Card comments">
            <div class="card-editor-comment-entry">
              <h3 class="card-editor-comments-title">Comments</h3>
              <div class="card-editor-comment-entry-row">
                <MdEditor
                  ref="commentEditorRef"
                  :model-value="newCommentText"
                  aria-label="Comment"
                  :max-length="maxCommentLength"
                  min-height="3rem"
                  :show-toolbar="false"
                  :image-context="cardImageContext"
                  :image-remove="removeCommentImage"
                  :image-upload="cardImageUpload"
                  :image-upload-cancel="attachments.cancel"
                  @update:model-value="updateCommentDraftFromEditor"
                  @user-edit="applyUserCommentEdit"
                  @focus="handleCommentEditorFocus"
                  @blur="handleCommentEditorBlur"
                  @escape="closeCardEditor"
                  @toolbar-state-change="handleCommentToolbarStateChange"
                  @plain-text-mode-change="handleCommentPlainTextModeChange"
                  @image-upload-blocking-change="handleCommentImageBlockingChange"
                />
                <button
                  type="button"
                  class="btn card-editor-comment-add-button"
                  :disabled="newCommentText.trim().length === 0 || commentsBusy || commentImageBlocking"
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
                <aside class="card-editor-comment-author-rail" :aria-label="comment.isSystem ? 'System comment' : 'Comment author'">
                  <span v-if="comment.isSystem" class="card-editor-comment-author-name">System</span>
                  <span v-else class="card-editor-comment-author">
                    <UserAvatar
                      :image-relative-path="comment.authorImageRelativePath ?? null"
                      :display-name="comment.authorDisplayName ?? 'Unknown user'"
                      size="sm"
                      class="card-editor-comment-author-avatar"
                    />
                    <span class="card-editor-comment-author-name">{{ comment.authorDisplayName ?? 'Unknown user' }}</span>
                  </span>
                </aside>
                <div class="card-editor-comment-content" :class="{ 'card-editor-comment-content--editing': editingCommentId === comment.id }">
                  <BoDropdown
                    v-if="(canEditComment(comment) || canDeleteComment(comment)) && editingCommentId !== comment.id"
                    class="card-editor-comment-actions"
                    align="right"
                    label="Comment actions"
                    :teleport="false"
                    :icon="Ellipsis"
                    :icon-only="true"
                    :icon-size="14"
                  >
                    <template #default="{ close }">
                      <button v-if="canEditComment(comment)" type="button" class="bo-dropdown-item" :disabled="commentsBusy || imageUploadBlocking" @click="close(); void beginCommentEdit(comment)">
                        Edit
                      </button>
                      <button v-if="canDeleteComment(comment)" type="button" class="bo-dropdown-item" :disabled="commentsBusy || imageUploadBlocking" @click="close(); void deleteComment(comment)">
                        Delete
                      </button>
                    </template>
                  </BoDropdown>
                  <div v-if="editingCommentId === comment.id" class="card-editor-comment-edit" @keydown="handleCommentEditKeydown">
                    <p v-if="editedCommentDeleted" role="status">This comment was deleted. Copy any text you want to keep before cancelling.</p>
                    <MdEditor
                      :ref="setEditedCommentEditorRef"
                      :model-value="editedCommentText"
                      aria-label="Edit comment"
                      :max-length="maxCommentLength"
                      min-height="3rem"
                      :show-toolbar="false"
                      :image-context="cardImageContext"
                      :image-remove="removeEditedCommentImage"
                      :image-upload="editedCommentDeleted ? null : cardImageUpload"
                      :image-upload-cancel="attachments.cancel"
                      @update:model-value="updateEditedCommentText"
                      @user-edit="applyUserEditedCommentChange"
                      @focus="handleEditedCommentFocus"
                      @blur="handleEditedCommentBlur"
                      @escape="cancelCommentEdit"
                      @toolbar-state-change="handleEditedCommentToolbarStateChange"
                      @plain-text-mode-change="handleEditedCommentPlainTextModeChange"
                      @image-upload-blocking-change="handleEditedCommentImageBlockingChange"
                    />
                    <div class="card-editor-comment-edit-actions">
                      <button type="button" class="btn" :disabled="editedCommentDeleted || commentsBusy || editedCommentImageBlocking || editedCommentText.trim().length === 0" @click="saveCommentEdit">Save comment</button>
                      <button type="button" class="btn btn--secondary" :disabled="editedCommentImageBlocking" @click="cancelCommentEdit">Cancel</button>
                    </div>
                  </div>
                  <MdViewer
                    v-else
                    class="card-editor-comment-body"
                    :model-value="comment.text"
                    aria-label="Comment content"
                    :max-length="maxCommentLength"
                    min-height="1.5rem"
                    :image-context="cardImageContext"
                  />
                  <time
                    class="card-editor-comment-timestamp"
                    :datetime="comment.editedAtUtc ?? comment.postedAtUtc"
                  >
                    <span v-if="comment.editedAtUtc" :title="`Edited ${formatCommentDateTime(comment.editedAtUtc)}`">Edited · </span>
                    {{ formatCommentDateTime(comment.editedAtUtc ?? comment.postedAtUtc) }}
                  </time>
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
              :disabled="cardBusy"
              :teleport="false"
              :text="selectedCardTypeLabel"
            >
              <template #trigger>
                <span class="card-editor-type-trigger-content">
                  <span v-if="selectedCardTypeEmoji" class="bo-emoji" aria-hidden="true">{{ selectedCardTypeEmoji }}</span>
                  <span>{{ selectedCardTypeLabel }}</span>
                </span>
              </template>
              <template #default="{ close }">
                <button
                  v-for="cardType in cardTypes"
                  :key="cardType.id"
                  type="button"
                  class="bo-dropdown-item"
                  @click="setDraftCardTypeId(cardType.id, close)"
                >
                  <span class="bo-dropdown-item-main">
                    <span v-if="cardType.emoji" class="bo-emoji" aria-hidden="true">{{ cardType.emoji }}</span>{{ cardType.emoji ? ' ' : '' }}{{ cardType.name }}
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

          <p v-if="cardTypeWillChange" class="card-field-conversion-note">Fields will convert when you {{ isDuplicatingCard ? 'create the duplicate' : 'save' }}.</p>
          <CustomFieldValuesEditor
            ref="fieldValuesEditor"
            :definitions="customFieldDefinitions"
            :values="cardDraft.customFieldValues ?? {}"
            :errors="cardSaveError?.validationErrors ?? {}"
            :disabled="cardBusy"
            @change="updateDraftField"
          />

          <CardAttachments v-if="routeCardId !== null" :board-id="boardId" :card-id="routeCardId"
            :read-only="isDuplicatingCard" :duplicating="isDuplicatingCard" />

          <div v-if="!isDuplicatingCard" class="card-editor-option-section">
            <span class="card-editor-field-label">Created</span>
            <span>{{ formatCardDateTime(editingCard!.cardCreatedUtc) }}</span>
          </div>

          <div v-if="!isDuplicatingCard" class="card-editor-option-section">
            <span class="card-editor-field-label">Updated</span>
            <span>{{ formatCardDateTime(editingCard!.cardUpdatedUtc) }}</span>
          </div>

        </aside>
      </div>
    </template>
    <template #actions>
      <div v-if="cardDraft" class="editor-actions fixed-chrome-dialog-actions">
        <div class="fixed-chrome-dialog-actions-left">
          <button
            type="submit"
            class="btn"
            :disabled="cardBusy || imageUploadBlocking || (isDuplicatingCard && !canCreateDuplicate)"
            :aria-label="primaryActionLabel"
            :title="primaryActionLabel"
          >
            <Check :size="16" aria-hidden="true" />
            <span>{{ isDuplicatingCard ? 'Create' : 'Save' }}</span>
          </button>
          <button type="button" class="btn btn--secondary" aria-label="Cancel editing" title="Cancel" @click="closeCardEditor">
            <X :size="16" aria-hidden="true" />
            <span>Cancel</span>
          </button>
        </div>
      </div>
    </template>
  </FixedChromeDialog>
  <CardTypeChangeDialog
    v-if="pendingTypeChange"
    :preview="pendingTypeChange.preview"
    @confirm="confirmTypeChange"
    @cancel="cancelTypeChange"
  />
</template>

<script setup lang="ts">
import { Archive, ArrowRightLeft, Check, CircleAlert, Copy, Ellipsis, Trash2, X } from '@lucide/vue';
import { storeToRefs } from 'pinia';
import { computed, nextTick, ref, watch, type ComponentPublicInstance } from 'vue';
import { onBeforeRouteLeave, onBeforeRouteUpdate, useRoute, useRouter } from 'vue-router';
import MdEditor from '../../shared/components/MdEditor.vue';
import CardAttachments from './CardAttachments.vue';
import CustomFieldValuesEditor from './CustomFieldValuesEditor.vue';
import CardTypeChangeDialog from './CardTypeChangeDialog.vue';
import { buildCustomFieldValues } from '../utils/customFieldValues';
import type { CustomFieldDefinition, CardTypeChangePreview } from '../../shared/types/customFieldTypes';
import type { AppError } from '../../shared/types/appError';
import { useAttachmentStore } from '../stores/attachmentStore';
import MdEditorToolbar from '../../shared/components/MdEditorToolbar.vue';
import MdViewer from '../../shared/components/MdViewer.vue';
import BoDropdown from '../../shared/components/BoDropdown.vue';
import UserAvatar from '../../shared/components/UserAvatar.vue';
import CardTagEditor from './CardTagEditor.vue';
import CardSlickPicker from './CardSlickPicker.vue';
import CardExternalUrlEditor from './CardExternalUrlEditor.vue';
import CardTitleEditor from './CardTitleEditor.vue';
import FixedChromeDialog from '../../shared/components/FixedChromeDialog.vue';
import { useConfirm } from '../../shared/composables/useConfirm';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import { useBoardStore } from '../stores/boardStore';
import { useBoardMembersStore } from '../stores/boardMembersStore';
import { useCardStore } from '../stores/cardStore';
import { useCustomFieldStore } from '../stores/customFieldStore';
import { useCardTypeStore } from '../stores/cardTypeStore';
import { useCommentStore } from '../stores/commentStore';
import { useSlickStore } from '../stores/slickStore';
import { useTagStore } from '../stores/tagStore';
import { useAuthStore } from '../../shared/stores/authStore';
import { resolveSelectedCardTypeEmoji } from './cardTypeSelection';
import { mdEditorToolbarActions, type MdEditorToolbarActionEvent, type MdEditorToolbarActionId, type MdEditorToolbarActionState } from '../../shared/components/mdEditorToolbarActions';
import { createDisabledToolbarState, resolveActiveIsPlainTextMode, resolveActiveToolbarState } from './cardEditorSharedToolbar';
import { areCardEditModelsEqual, cloneCardEditModel, createCardEditModel } from './cardEditModel';
import type { Card, CardComment, CardEditModel } from '../../shared/types/boardTypes';
import { apiBase } from '../../shared/api/config';
import type {
  MarkdownImageActivation,
  MarkdownImageContext,
  MarkdownImageUpload,
  MarkdownImageUploadOptions
} from '../../shared/components/markdownImages';

const route = useRoute();
const router = useRouter();
const boardStore = useBoardStore();
const boardMembersStore = useBoardMembersStore();
const cardStore = useCardStore();
const cardTypeStore = useCardTypeStore();
const customFieldStore = useCustomFieldStore();
const commentStore = useCommentStore();
const slickStore = useSlickStore();
const tagStore = useTagStore();
const authStore = useAuthStore();
const { board, currentBoardId } = storeToRefs(boardStore);
const { busy: cardBusy } = storeToRefs(cardStore);
const { members: boardMembers, activeBoardId: boardMembersActiveBoardId } = storeToRefs(boardMembersStore);
const { cardTypes, activeBoardId: cardTypesActiveBoardId, systemCardType } = storeToRefs(cardTypeStore);
const { slicks, activeBoardId: slicksActiveBoardId } = storeToRefs(slickStore);
const { busy: commentsBusy } = storeToRefs(commentStore);
const { createCard: createCardAction, saveCard: saveCardAction, deleteCards, archiveCards } = cardStore;
const { loadCardComments, addCardComment: addCardCommentAction, updateCardComment, deleteCardComment } = commentStore;
const { loadMembers } = boardMembersStore;
const { loadCardTypes } = cardTypeStore;
const { loadSlicks } = slickStore;
const { ensureTagsExist } = tagStore;
const { confirm } = useConfirm();
const feedback = useUiFeedbackStore();
const attachments = useAttachmentStore();
const maxDescriptionLength = 20_000;
const maxCommentLength = 4_000;
const cardDraft = ref<CardEditModel | null>(null);
const editorDialog = ref<InstanceType<typeof FixedChromeDialog> | null>(null);
const savingCard = ref(false);
const pendingTypeChange = ref<{
  preview: CardTypeChangePreview;
  cardId: number;
  draft: CardEditModel;
  closeAfterSave: boolean;
  duplicating: boolean;
} | null>(null);
const cardDraftBoardId = ref<number | null>(null);
const cardDraftId = ref<number | null>(null);
const cardDraftSource = ref<CardEditModel | null>(null);
const isCardDraftDirty = ref(false);
const customFieldDefinitions = ref<CustomFieldDefinition[]>([]);
const cardSaveError = ref<AppError | null>(null);
const cardErrorSummary = ref<HTMLElement | null>(null);
const fieldValuesEditor = ref<InstanceType<typeof CustomFieldValuesEditor> | null>(null);
const cardTypeWillChange = computed(() => cardDraft.value?.cardTypeId !== cardDraftSource.value?.cardTypeId);
const cardSummaryErrors = computed(() => Object.fromEntries(Object.entries(cardSaveError.value?.validationErrors ?? {})
  .filter(([path]) => !customFieldDefinitions.value.some(field => path === `customFieldValues.${field.code}`))));
const isDuplicatingCard = ref(false);
const newCommentText = ref('');
const isCommentDraftDirty = ref(false);
const descriptionEditorRef = ref<InstanceType<typeof MdEditor> | null>(null);
const commentEditorRef = ref<InstanceType<typeof MdEditor> | null>(null);
const editedCommentEditorRef = ref<InstanceType<typeof MdEditor> | null>(null);
const editingCommentId = ref<number | null>(null);
const editingCommentSnapshot = ref<CardComment | null>(null);
const editedCommentDeleted = computed(() => editingCommentId.value !== null && commentStore.isCommentDeleted(editingCommentId.value));
const editedCommentText = ref('');
const editedCommentSourceText = ref('');
const isEditedCommentDirty = ref(false);
const editedCommentFocused = ref(false);
const editedCommentToolbarState = ref<Partial<Record<MdEditorToolbarActionId, MdEditorToolbarActionState>>>({});
const editedCommentIsPlainTextMode = ref(false);
const editedCommentImageBlocking = ref(false);
const activeEditor = ref<'description' | 'comment' | 'commentEdit'>('description');
const descriptionEditorFocused = ref(false);
const commentEditorFocused = ref(false);
const descriptionToolbarState = ref<Partial<Record<MdEditorToolbarActionId, MdEditorToolbarActionState>>>({});
const commentToolbarState = ref<Partial<Record<MdEditorToolbarActionId, MdEditorToolbarActionState>>>({});
const descriptionIsPlainTextMode = ref(false);
const commentIsPlainTextMode = ref(false);
const descriptionImageBlocking = ref(false);
const commentImageBlocking = ref(false);

const hasUnsavedChanges = computed(() => isCardDraftDirty.value || isCommentDraftDirty.value || isEditedCommentDirty.value);
const isTransferDisabled = computed(() => hasUnsavedChanges.value || attachments.busy);
const transferActionTitle = computed(() => isTransferDisabled.value
  ? 'Save or discard unsaved changes before moving this card.'
  : 'Move this card to another board.');
const isDuplicateDisabled = computed(() => hasUnsavedChanges.value || attachments.busy);
const duplicateActionTitle = computed(() => isDuplicateDisabled.value
  ? 'Save or discard unsaved changes before duplicating this card.'
  : 'Duplicate this card.');
const canCreateDuplicate = computed(() => (
  Boolean(cardDraft.value?.title.trim()) && cardDraft.value?.cardTypeId !== null
));
const primaryActionLabel = computed(() => isDuplicatingCard.value ? 'Create duplicate card' : 'Save card');
const imageUploadBlocking = computed(() => descriptionImageBlocking.value || commentImageBlocking.value || editedCommentImageBlocking.value);
const cardImageContext = computed<MarkdownImageContext | null>(() => {
  const cardId = routeCardId.value;
  if (!attachments.supported || cardId === null) {
    return null;
  }
  return {
    apiBaseUrl: apiBase,
    boardId: boardId.value,
    cardId,
    refreshKey: attachments.revision
  };
});
const cardImageUpload = computed<MarkdownImageUpload | null>(() => {
  return isDuplicatingCard.value || !attachments.mutable ? null : uploadCardImage;
});

const routeCardId = computed<number | null>(() => {
  const raw = route.params.cardId;
  const parsed = typeof raw === 'string' ? Number.parseInt(raw, 10) : Number.NaN;
  return Number.isFinite(parsed) ? parsed : null;
});

const boardId = computed(() => currentBoardId.value!);

const editingCard = computed(() => cardStore.getCardById(routeCardId.value));
const cardComments = computed(() => {
  const savedComments = commentStore.getCommentsForCard(routeCardId.value);
  if (editedCommentDeleted.value && editingCommentSnapshot.value) {
    return [...savedComments, editingCommentSnapshot.value];
  }
  return savedComments;
});
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

  return selectedCardType.name;
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

  const cardAssignedUserDisplayName = editingCard.value?.assignedUserDisplayName;
  return cardAssignedUserDisplayName ?? `User #${cardDraft.value.assignedUserId}`;
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
  if (activeEditor.value === 'commentEdit') {
    return editedCommentToolbarState.value;
  }
  return resolveActiveToolbarState(
    activeEditor.value,
    descriptionToolbarState.value,
    commentToolbarState.value,
    disabledToolbarState.value
  );
});
const activeIsPlainTextMode = computed(() => {
  if (activeEditor.value === 'commentEdit') {
    return editedCommentIsPlainTextMode.value;
  }
  return resolveActiveIsPlainTextMode(
    activeEditor.value,
    descriptionIsPlainTextMode.value,
    commentIsPlainTextMode.value
  );
});
function setActiveEditor(editor: 'description' | 'comment' | 'commentEdit') {
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
  getActiveEditor()?.runToolbarAction(actionEvent);
}

function selectActiveEditorImage() {
  getActiveEditor()?.selectImage();
}

function toggleSharedToolbarPlainTextMode() {
  getActiveEditor()?.togglePlainTextMode();
}

function getActiveEditor() {
  if (activeEditor.value === 'commentEdit') {
    return editedCommentEditorRef.value;
  }
  if (activeEditor.value === 'comment') {
    return commentEditorRef.value;
  }
  return descriptionEditorRef.value;
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

function formatCardDateTime(value: string) {
  return formatCommentDateTime(value);
}

function clearDraft() {
  clearCardDraft();
  resetCommentDraft();
  resetCommentEdit();
  activeEditor.value = 'description';
  descriptionEditorFocused.value = false;
  commentEditorFocused.value = false;
  descriptionImageBlocking.value = false;
  commentImageBlocking.value = false;
}

function resetCommentDraft() {
  newCommentText.value = '';
  isCommentDraftDirty.value = false;
}

function canEditComment(comment: CardComment) {
  return !comment.isSystem && boardStore.currentUserRole !== null && comment.authorUserId === authStore.user?.id && !commentStore.isCommentDeleted(comment.id);
}

function canDeleteComment(comment: CardComment) {
  return boardStore.currentUserRole !== null && !commentStore.isCommentDeleted(comment.id)
    && ((!comment.isSystem && comment.authorUserId === authStore.user?.id) || boardStore.isCurrentUserOwner);
}

async function deleteComment(comment: CardComment) {
  const targetBoardId = boardId.value;
  const cardId = routeCardId.value;
  if (cardId === null || !canDeleteComment(comment) || commentsBusy.value || imageUploadBlocking.value) {
    return;
  }
  const confirmed = await confirm({
    title: 'Delete comment',
    message: comment.isSystem
      ? `Delete this system comment?\n\n${comment.text.slice(0, 200)}`
      : `Delete this comment by ${comment.authorDisplayName ?? 'Unknown user'}?\n\n${comment.text.slice(0, 200)}`,
    confirmLabel: 'Delete',
    danger: true
  });
  if (!confirmed || boardId.value !== targetBoardId || routeCardId.value !== cardId
    || !canDeleteComment(comment) || commentsBusy.value || imageUploadBlocking.value) {
    return;
  }
  await deleteCardComment(targetBoardId, cardId, comment.id);
}

function setEditedCommentEditorRef(instance: Element | ComponentPublicInstance | null) {
  editedCommentEditorRef.value = instance as InstanceType<typeof MdEditor> | null;
}

async function beginCommentEdit(comment: CardComment) {
  if (!canEditComment(comment) || commentsBusy.value || imageUploadBlocking.value) {
    return;
  }
  if (isEditedCommentDirty.value) {
    const discard = await confirm({ title: 'Discard comment changes', message: 'Discard your unsaved comment changes?', confirmLabel: 'Discard', danger: true });
    if (!discard) {
      return;
    }
  }
  resetCommentEdit();
  editingCommentId.value = comment.id;
  editingCommentSnapshot.value = { ...comment };
  editedCommentText.value = comment.text;
  editedCommentSourceText.value = comment.text;
  activeEditor.value = 'commentEdit';
  await nextTick();
  editedCommentEditorRef.value?.focus();
}

function resetCommentEdit() {
  editingCommentId.value = null;
  editingCommentSnapshot.value = null;
  editedCommentText.value = '';
  editedCommentSourceText.value = '';
  isEditedCommentDirty.value = false;
  editedCommentFocused.value = false;
  editedCommentToolbarState.value = {};
  editedCommentIsPlainTextMode.value = false;
  editedCommentImageBlocking.value = false;
  if (activeEditor.value === 'commentEdit') {
    activeEditor.value = 'description';
  }
}

function cancelCommentEdit() {
  if (editedCommentImageBlocking.value) {
    return;
  }
  resetCommentEdit();
}

function updateEditedCommentText(value: string) {
  if (editedCommentFocused.value && editedCommentText.value !== value) {
    isEditedCommentDirty.value = value !== editedCommentSourceText.value;
  }
  editedCommentText.value = value;
}

function applyUserEditedCommentChange(value: string) {
  editedCommentText.value = value;
  isEditedCommentDirty.value = value !== editedCommentSourceText.value;
}

function handleEditedCommentFocus() {
  editedCommentFocused.value = true;
  activeEditor.value = 'commentEdit';
}

function handleEditedCommentBlur() {
  editedCommentFocused.value = false;
}

function handleEditedCommentToolbarStateChange(state: Partial<Record<MdEditorToolbarActionId, MdEditorToolbarActionState>>) {
  editedCommentToolbarState.value = state;
}

function handleEditedCommentPlainTextModeChange(value: boolean) {
  editedCommentIsPlainTextMode.value = value;
}

function handleEditedCommentImageBlockingChange(value: boolean) {
  editedCommentImageBlocking.value = value;
}

async function removeEditedCommentImage(): Promise<boolean> {
  return await confirm({ title: 'Remove image', message: 'Remove this image from the comment?', confirmLabel: 'Remove' });
}

async function saveCommentEdit() {
  const cardId = routeCardId.value;
  const commentId = editingCommentId.value;
  if (cardId === null || commentId === null || editedCommentDeleted.value || commentsBusy.value || editedCommentImageBlocking.value) {
    return;
  }
  const submittedDraft = editedCommentText.value;
  const submittedText = isEditedCommentDirty.value ? submittedDraft : editedCommentSourceText.value;
  const editorRoute = router.currentRoute.value;
  const result = await updateCardComment(boardId.value, cardId, commentId, submittedText);
  if (!result?.ok || router.currentRoute.value !== editorRoute || editingCommentId.value !== commentId) {
    return;
  }
  if (editedCommentText.value === submittedDraft) {
    resetCommentEdit();
    return;
  }
  editedCommentSourceText.value = result.data.text;
  isEditedCommentDirty.value = editedCommentText.value !== result.data.text;
}

function handleCommentEditKeydown(event: KeyboardEvent) {
  if ((event.ctrlKey || event.metaKey) && !event.altKey && !event.shiftKey && event.key.toLowerCase() === 's') {
    event.preventDefault();
    event.stopPropagation();
    if (!event.repeat) {
      void saveCommentEdit();
    }
  }
}

async function confirmDiscardUnsavedChanges() {
  if (attachments.busy) {
    const cancelUpload = await confirm({ title: 'Cancel upload', message: 'Cancel the active upload and leave? Completed attachments will be kept.', confirmLabel: 'Cancel upload' });
    if (!cancelUpload) { return false; }
    attachments.cancel();
  }
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

function handleCardEditorKeydown(event: KeyboardEvent) {
  const isSaveShortcut = (event.ctrlKey || event.metaKey)
    && !event.altKey
    && !event.shiftKey
    && event.key.toLowerCase() === 's';
  if (!isSaveShortcut) {
    return;
  }

  event.preventDefault();
  if (event.repeat || cardBusy.value) {
    return;
  }

  void saveCardFromShortcut();
}

async function closeCardEditorWithoutPrompt() {
  await closeCardEditorInternal(true);
}

async function closeCardEditorInternal(skipUnsavedGuard: boolean) {
  if (skipUnsavedGuard && attachments.busy) {
    const cancelUpload = await confirm({ title: 'Cancel upload', message: 'Cancel the active upload and leave? Completed attachments will be kept.', confirmLabel: 'Cancel upload' });
    if (!cancelUpload) { return; }
    attachments.cancel();
  }
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

function applyUserCommentEdit(value: string) {
  newCommentText.value = value;
  isCommentDraftDirty.value = true;
}

function handleDescriptionImageBlockingChange(blocking: boolean) {
  descriptionImageBlocking.value = blocking;
}

function handleCommentImageBlockingChange(blocking: boolean) {
  commentImageBlocking.value = blocking;
}

async function removeDescriptionImage(image: MarkdownImageActivation): Promise<boolean> {
  return await removeDraftImage(image, 'description');
}

async function removeCommentImage(image: MarkdownImageActivation): Promise<boolean> {
  return await removeDraftImage(image, 'comment');
}

async function removeDraftImage(image: MarkdownImageActivation, location: 'description' | 'comment'): Promise<boolean> {
  const attachmentFileName = image.attachmentFileName?.toUpperCase();
  const attachment = attachmentFileName
    ? attachments.items.find(item => item.originalFileName.toUpperCase() === attachmentFileName)
    : undefined;
  let message = `Remove this image from the ${location}?`;
  if (attachment) {
    message = `Remove this image from the ${location} and delete attachment "${attachment.originalFileName}"?`;
  }
  const confirmed = await confirm({
    title: 'Remove image',
    message,
    confirmLabel: 'Remove',
    danger: true
  });
  if (!confirmed || !attachment) {
    return confirmed;
  }
  return await attachments.remove(attachment.id);
}

async function uploadCardImage(
  files: File[],
  onProgress: (file: File, percent: number) => void,
  options: MarkdownImageUploadOptions
) {
  const results = files.map(() => null as { fileName: string } | null);
  const availableByName = new Map(attachments.items.map(attachment =>
    [attachment.originalFileName.toUpperCase(), attachment.originalFileName]));
  const reservedNames = new Set(availableByName.keys());
  const targetNames = files.map(() => '');
  const filesToUpload = new Map<string, { file: File; sources: File[] }>();

  files.forEach((file, index) => {
    const normalisedFileName = file.name.toUpperCase();
    const existingFileName = availableByName.get(normalisedFileName);
    if (options.reuseExisting && existingFileName) {
      results[index] = { fileName: existingFileName };
      targetNames[index] = existingFileName;
      onProgress(file, 100);
      return;
    }

    const targetName = options.reuseExisting
      ? file.name
      : reserveUniqueAttachmentFileName(file.name, reservedNames);
    const targetKey = targetName.toUpperCase();
    targetNames[index] = targetName;
    reservedNames.add(targetKey);
    const planned = filesToUpload.get(targetKey);
    if (planned) {
      planned.sources.push(file);
      return;
    }
    const uploadFile = targetName === file.name
      ? file
      : new File([file], targetName, { type: file.type, lastModified: file.lastModified });
    filesToUpload.set(targetKey, { file: uploadFile, sources: [file] });
  });

  const uploadPlans = [...filesToUpload.values()];
  const sourcesByUploadFile = new Map(uploadPlans.map(plan => [plan.file, plan.sources]));
  const uploaded = await attachments.upload(uploadPlans.map(plan => plan.file), (file, percent) => {
    for (const source of sourcesByUploadFile.get(file) ?? []) {
      onProgress(source, percent);
    }
  });
  for (const attachment of uploaded) {
    availableByName.set(attachment.originalFileName.toUpperCase(), attachment.originalFileName);
  }
  for (const attachment of attachments.items) {
    availableByName.set(attachment.originalFileName.toUpperCase(), attachment.originalFileName);
  }

  files.forEach((file, index) => {
    const fileName = availableByName.get(targetNames[index]!.toUpperCase());
    if (fileName) {
      results[index] = { fileName };
    }
  });
  return results;
}

function reserveUniqueAttachmentFileName(fileName: string, reservedNames: Set<string>): string {
  if (!reservedNames.has(fileName.toUpperCase())) {
    return fileName;
  }
  const extensionIndex = fileName.lastIndexOf('.');
  const hasExtension = extensionIndex > 0;
  const extension = hasExtension ? fileName.slice(extensionIndex) : '';
  const baseName = hasExtension ? fileName.slice(0, extensionIndex) : fileName;
  let sequence = 2;
  while (true) {
    const suffix = ` (${sequence})`;
    const availableBaseLength = Math.max(1, 255 - extension.length - suffix.length);
    const candidate = `${baseName.slice(0, availableBaseLength)}${suffix}${extension}`;
    if (!reservedNames.has(candidate.toUpperCase())) {
      return candidate;
    }
    sequence++;
  }
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

function updateDraftField(code: string, value: string) {
  if (!cardDraft.value) return;
  patchFromUser({ customFieldValues: { ...cardDraft.value.customFieldValues, [code]: value } });
  if (cardSaveError.value?.validationErrors) {
    const errors = { ...cardSaveError.value.validationErrors };
    delete errors[`customFieldValues.${code}`];
    cardSaveError.value = Object.keys(errors).length ? { ...cardSaveError.value, validationErrors: errors } : null;
  }
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
  pendingTypeChange.value = null;
  customFieldDefinitions.value = [];
  cardSaveError.value = null;
  cardStore.clearSaveError();
  cardDraft.value = null;
  cardDraftBoardId.value = null;
  cardDraftId.value = null;
  cardDraftSource.value = null;
  isCardDraftDirty.value = false;
  isDuplicatingCard.value = false;
}

function initializeDraftFromCard(sourceBoardId: number, card: Card) {
  if (cardDraftBoardId.value === sourceBoardId && cardDraftId.value === card.id) {
    return false;
  }

  pendingTypeChange.value = null;
  const initialModel = createCardEditModel(card);
  customFieldDefinitions.value = customFieldStore.resolve(card.cardTypeId).map(field => ({ ...field }));
  cardSaveError.value = null;
  cardDraft.value = initialModel;
  cardDraftSource.value = cloneCardEditModel(initialModel);
  cardDraftBoardId.value = sourceBoardId;
  cardDraftId.value = card.id;
  isCardDraftDirty.value = false;
  isDuplicatingCard.value = false;
  return true;
}

function beginDuplicate() {
  if (!cardDraft.value || isDuplicateDisabled.value) {
    return;
  }

  const duplicateDraft = cloneCardEditModel(cardDraft.value);
  cardDraft.value = duplicateDraft;
  cardDraftSource.value = cloneCardEditModel(duplicateDraft);
  isCardDraftDirty.value = false;
  isDuplicatingCard.value = true;
  resetCommentDraft();
  activeEditor.value = 'description';
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
  resetCommentEdit();
  return true;
}

async function saveCard() {
  await saveCardDraft(true);
}

async function saveCardFromShortcut() {
  if (savingCard.value || !editorDialog.value?.reportValidity()) return;
  await saveCardDraft(false);
}

async function saveCardDraft(closeAfterSave: boolean) {
  const draft = cardDraft.value;
  const cardId = routeCardId.value;
  if (!draft || draft.cardTypeId === null || cardId === null || savingCard.value || pendingTypeChange.value
    || cardBusy.value || imageUploadBlocking.value) return;

  const submittedDraft = {
    ...cloneCardEditModel(draft),
    customFieldValues: buildCustomFieldValues(customFieldDefinitions.value, draft.customFieldValues ?? {})
  };
  const duplicating = isDuplicatingCard.value;
  savingCard.value = true;
  try {
    if (cardTypeWillChange.value) {
      const preview = await cardStore.previewTypeChange(boardId.value, cardId, draft.cardTypeId, submittedDraft.customFieldValues);
      if (!preview.ok) {
        savingCard.value = false;
        await showCardSaveError(preview.error);
        return;
      }
      if (preview.data.requiresConfirmation) {
        pendingTypeChange.value = { preview: preview.data, cardId, draft: submittedDraft, closeAfterSave, duplicating };
        return;
      }
    }
    await persistCardDraft(cardId, submittedDraft, closeAfterSave, duplicating);
  } finally {
    savingCard.value = false;
  }
}

function cancelTypeChange() {
  pendingTypeChange.value = null;
}

async function confirmTypeChange() {
  const pending = pendingTypeChange.value;
  if (!pending || savingCard.value) return;
  pendingTypeChange.value = null;
  savingCard.value = true;
  try {
    await persistCardDraft(pending.cardId, pending.draft, pending.closeAfterSave, pending.duplicating);
  } finally {
    savingCard.value = false;
  }
}

async function persistCardDraft(cardId: number, submittedDraft: CardEditModel, closeAfterSave: boolean, duplicating: boolean) {
  if (duplicating) {
    const created = await createCardAction(submittedDraft, { duplicateFromCardId: cardId, inlineErrors: true });
    savingCard.value = false;
    if (created?.ok) {
      feedback.showToast('Created successfully.');
      await closeCardEditorWithoutPrompt();
    } else if (created && !created.ok) {
      await showCardSaveError(created.error);
    }
    return;
  }

  const saved = await saveCardAction(cardId, submittedDraft, { inlineErrors: true });
  if (saved) syncDraftAfterSave(cardId);
  savingCard.value = false;
  await nextTick();
  if (!saved) {
    await showCardSaveError(cardStore.saveError);
    return;
  }
  cardSaveError.value = null;
  feedback.showToast('Saved successfully.');
  if (closeAfterSave) {
    if (isEditedCommentDirty.value) {
      await closeCardEditor();
      return;
    }
    await closeCardEditorWithoutPrompt();
  }
}

async function showCardSaveError(error: AppError | null) {
  cardSaveError.value = error;
  await nextTick();
  if (Object.keys(cardSummaryErrors.value).length || !error?.validationErrors) cardErrorSummary.value?.focus();
  else fieldValuesEditor.value?.focusError();
}

function syncDraftAfterSave(cardId: number) {
  const savedCard = cardStore.getCardById(cardId);
  const currentDraft = cardDraft.value;
  if (!savedCard || !currentDraft) {
    return;
  }

  const savedModel = createCardEditModel(savedCard);
  customFieldDefinitions.value = customFieldStore.resolve(savedCard.cardTypeId).map(field => ({ ...field }));
  cardDraftSource.value = cloneCardEditModel(savedModel);
  cardDraft.value = cloneCardEditModel(savedModel);
  isCardDraftDirty.value = false;
}

async function addComment() {
  const cardId = routeCardId.value;
  if (!cardDraft.value || commentsBusy.value || commentImageBlocking.value || cardId === null) {
    return;
  }

  const submittedDraft = newCommentText.value;
  const editorRoute = router.currentRoute.value;
  const text = submittedDraft.trim().slice(0, maxCommentLength);
  if (text.length === 0) {
    return;
  }

  const result = await addCardCommentAction(boardId.value, cardId, text);
  if (!result?.ok || router.currentRoute.value !== editorRoute || newCommentText.value !== submittedDraft) {
    return;
  }

  resetCommentDraft();
}

async function openCardTransfer() {
  const cardId = routeCardId.value;
  if (cardId === null || isTransferDisabled.value) {
    return;
  }

  await router.push({
    name: 'board-card-transfer',
    params: { boardId: boardId.value, cardId }
  });
}

async function deleteEditingCard() {
  if (attachments.busy) { return; }
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

  const deleted = await deleteCards([cardId]);
  if (deleted) {
    await closeCardEditorWithoutPrompt();
  }
}

async function archiveEditingCard() {
  if (attachments.busy) { return; }
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

  const archived = await archiveCards([cardId]);
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

    const lookupsLoaded = await ensureEditorLookupsLoaded(nextBoardId, () => cancelled);
    if (!lookupsLoaded || cancelled) {
      return;
    }

    const draftInitialized = initializeDraftForCard(nextBoardId, nextCard);
    if (draftInitialized || commentStore.commentsByCardId[nextCard.id] === undefined) {
      await loadCardComments(nextBoardId, nextCard.id);
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

</script>

<style scoped>
.card-editor-dialog :deep(.fixed-chrome-dialog__body) { display: flex; flex-direction: column; }
.card-save-errors { color: var(--bo-colour-danger-ink); font-size: 0.85rem; max-height: 20dvh; overflow-y: auto; }
.card-save-errors p { display: flex; gap: 0.25rem; align-items: flex-start; margin: 0 0 0.3rem; }
.card-save-errors svg { flex-shrink: 0; }
.card-field-conversion-note { margin: 0; font-size: 0.8rem; color: var(--bo-ink-subtle); }

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
  color: var(--bo-ink-muted);
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
  background: color-mix(in srgb, var(--bo-surface-base) 92%, var(--bo-surface-muted) 8%);
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
  min-height: 3rem;
  padding-right: 3.9rem;
}

.card-editor-comment-timestamp {
  position: absolute;
  right: 0;
  bottom: 0;
  font-size: 0.8rem;
  color: var(--bo-ink-muted);
  line-height: 1.1;
  white-space: nowrap;
}

.card-editor-comment-actions {
  position: absolute;
  top: 0;
  right: 0;
}

.card-editor-comment-edit {
  display: flex;
  flex-direction: column;
  padding-bottom: 1.5rem;
}

.card-editor-comment-content--editing {
  padding-right: 0;
}

.card-editor-comment-edit-actions {
  display: flex;
  gap: 0.4rem;
  margin-top: 0.4rem;
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

.card-editor-comment-entry :deep(.md-editor),
.card-editor-comment-edit :deep(.md-editor) {
  flex: 0 0 auto;
  min-height: fit-content;
  width: 100%;
}

.card-editor-comment-entry :deep(.md-editor-input),
.card-editor-comment-edit :deep(.md-editor-input),
.card-editor-comment-entry :deep(.md-editor-content),
.card-editor-comment-edit :deep(.md-editor-content) {
  flex: 0 0 auto;
  min-height: fit-content;
  overflow: visible;
  width: 100%;
}

.card-editor-comment-entry :deep(.md-editor-content .tiptap),
.card-editor-comment-edit :deep(.md-editor-content .tiptap),
.card-editor-comment-entry :deep(.md-editor-textarea),
.card-editor-comment-edit :deep(.md-editor-textarea) {
  height: auto;
  max-height: none;
  overflow-y: visible;
  width: 100%;
}

.card-editor-comment-entry :deep(.md-editor-content .tiptap),
.card-editor-comment-edit :deep(.md-editor-content .tiptap),
.card-editor-comment-entry :deep(.md-editor-textarea),
.card-editor-comment-edit :deep(.md-editor-textarea),
.card-editor-description-field :deep(.md-editor-content .tiptap),
.card-editor-description-field :deep(.md-editor-textarea) {
  padding-top: 0.3rem;
  padding-bottom: 0.3rem;
}

.card-editor-comment-entry :deep(.md-editor-content .tiptap > :first-child),
.card-editor-comment-edit :deep(.md-editor-content .tiptap > :first-child),
.card-editor-description-field :deep(.md-editor-content .tiptap > :first-child) {
  margin-top: 0;
}

.card-editor-comment-entry :deep(.md-editor-content .tiptap > :last-child),
.card-editor-comment-edit :deep(.md-editor-content .tiptap > :last-child),
.card-editor-description-field :deep(.md-editor-content .tiptap > :last-child) {
  margin-bottom: 0;
}

.card-editor-comment-body {
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

.card-editor-type-trigger-content {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
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
  overflow-x: hidden;
  overflow-y: auto;
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

.card-editor-options :deep(.card-editor-field-label) {
  font-size: 0.85rem;
  color: var(--bo-ink-muted);
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
  color: var(--bo-colour-danger-ink);
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

  :deep(.fixed-chrome-dialog__body) {
    padding-right: 0.75rem;
    padding-left: 0.75rem;
  }
}
</style>
