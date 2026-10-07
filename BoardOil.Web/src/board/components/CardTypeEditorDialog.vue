<template>
  <FixedChromeDialog
    class="card-type-dialog"
    :class="{ 'card-type-dialog--fields': showCustomFields }"
    :body-mode="showCustomFields ? 'managed' : 'scroll'"
    :open="isCreateMode || editingCardType !== null"
    :title="dialogTitle"
    :close-label="isCreateMode ? 'Cancel creating' : 'Cancel editing'"
    @close="closeDialog"
    @submit="saveCardType"
  >
    <template v-if="draftName !== null && draftStyle !== null">
      <div v-if="hasSummaryError" ref="errorSummary" class="error card-type-errors" role="alert" tabindex="-1">
        <p v-if="Object.keys(summaryErrors).length === 0"><CircleAlert :size="14" aria-hidden="true" /><span>{{ editorError?.message }}</span></p>
        <p v-for="(messages, path) in summaryErrors" :key="path"><CircleAlert :size="14" aria-hidden="true" /><span>{{ messages.join(' ') }}</span></p>
      </div>
      <div v-if="fieldImpacts.length" class="card-type-preview" role="status" aria-label="Field change warnings">
        <TriangleAlert :size="16" aria-hidden="true" />
        <div>
          <p v-for="impact in fieldImpacts" :key="impact.fieldCode">
            <template v-if="impact.change === 'deleted'">Deleted field '{{ impact.displayName }}' will be removed from {{ impact.cardCount }} {{ impact.cardCount === 1 ? 'card' : 'cards' }}.</template>
            <template v-else>Changes to '{{ impact.displayName }}' will replace values on {{ impact.cardCount }} {{ impact.cardCount === 1 ? 'card' : 'cards' }}.</template>
          </p>
          <p>Removed values will be saved to comments.</p>
        </div>
      </div>
      <p v-else-if="previewError && !previewError.validationErrors" class="error card-type-preview-error" role="alert">
        <CircleAlert :size="14" aria-hidden="true" /> Unable to preview field changes.
      </p>
      <div class="card-type-columns">
        <section class="card-type-appearance" aria-labelledby="card-type-appearance-heading">
          <div class="card-type-appearance-header">
            <h4 id="card-type-appearance-heading" class="card-type-section-heading">Appearance</h4>
            <button v-if="!showCustomFields" type="button" class="btn btn--secondary card-type-fields-toggle" :disabled="busy" @click="openCustomFields">
              Custom Fields &gt;
            </button>
          </div>
          <div class="card-type-name-control">
            <label>
              Name
              <input
                v-model="draftName"
                ref="nameInput"
                :autofocus="isDuplicateMode"
                :aria-invalid="Boolean(validationErrors.name)"
                :aria-describedby="validationErrors.name ? 'card-type-name-error' : undefined"
                maxlength="40"
                :placeholder="isCreateMode ? 'New card type name' : 'Card type name'"
                :disabled="busy"
                autocomplete="off"
                data-lpignore="true"
              />
            </label>
            <small v-if="validationErrors.name" id="card-type-name-error" class="error card-type-inline-error"><CircleAlert :size="12" aria-hidden="true" /><span>{{ validationErrors.name.join(' ') }}</span></small>
          </div>
          <div class="card-types-dialog-preview">
            <span class="badge">Preview</span>
            <article class="card-type-preview-card" :class="previewCardStyleClasses" :style="previewCardStyle">
              <div class="card-header">
                <strong class="card-title">{{ previewTitle }}</strong>
                <span class="card-id">#123</span>
              </div>
            </article>
          </div>

          <label>
            Emoji
            <div class="card-types-emoji-picker-wrap">
              <EmojiPickerDropdown v-model="draftEmoji" :disabled="busy" :teleport="false" placeholder="Select emoji" />
            </div>
          </label>

          <label>
            Style
            <select :value="draftStyle.styleName" :disabled="busy" @change="onStyleNameChange">
              <option value="auto">Auto</option>
              <option value="presets">Presets</option>
              <option value="solid">Solid</option>
              <option value="gradient">Gradient</option>
            </select>
          </label>

          <template v-if="draftStyle.styleName === 'presets'">
            <label>
              Preset
              <div class="card-types-preset-picker" role="radiogroup" aria-label="Card type preset colour">
                <button
                  v-for="preset in presetColours"
                  :key="preset.cssVar"
                  type="button"
                  class="card-types-preset-swatch"
                  :class="{ 'card-types-preset-swatch--selected': draftStyle.presetIndex === preset.index }"
                  :style="{ backgroundColor: preset.cssValue }"
                  :disabled="busy"
                  :aria-pressed="draftStyle.presetIndex === preset.index"
                  :aria-label="`Preset ${preset.index + 1}`"
                  @click="setDraftStyleField('presetIndex', preset.index)"
                />
              </div>
            </label>
          </template>

          <template v-else-if="draftStyle.styleName === 'solid'">
            <label>
              Background Color
              <input
                type="color"
                class="card-types-colour-input"
                :value="draftStyle.backgroundColor"
                :disabled="busy"
                @input="onBackgroundColorInput"
              />
            </label>
          </template>

          <template v-else-if="draftStyle.styleName === 'gradient'">
            <label>
              Left Color
              <input
                type="color"
                class="card-types-colour-input"
                :value="draftStyle.leftColor"
                :disabled="busy"
                @input="onLeftColorInput"
              />
            </label>
            <label>
              Right Color
              <input
                type="color"
                class="card-types-colour-input"
                :value="draftStyle.rightColor"
                :disabled="busy"
                @input="onRightColorInput"
              />
            </label>
          </template>

          <label v-if="draftStyle.styleName === 'solid' || draftStyle.styleName === 'gradient'">
            Text Color Mode
            <select :value="draftStyle.textColorMode" :disabled="busy" @change="onTextModeChange">
              <option value="auto">Auto Contrast</option>
              <option value="custom">Custom</option>
            </select>
          </label>

          <label v-if="(draftStyle.styleName === 'solid' || draftStyle.styleName === 'gradient') && draftStyle.textColorMode === 'custom'">
            Text Color
            <input
              type="color"
              class="card-types-colour-input"
              :value="draftStyle.textColor"
              :disabled="busy"
              @input="onTextColorInput"
            />
          </label>

          <label v-if="draftStyle.styleName === 'solid' || draftStyle.styleName === 'gradient'">
            Border
            <select :value="draftStyle.borderMode" :disabled="busy" @change="onBorderModeChange">
              <option value="auto">Auto</option>
              <option value="custom">Custom</option>
              <option value="none">None</option>
            </select>
          </label>

          <label v-if="(draftStyle.styleName === 'solid' || draftStyle.styleName === 'gradient') && draftStyle.borderMode === 'custom'">
            Border Color
            <input
              type="color"
              class="card-types-colour-input"
              :value="draftStyle.borderColor"
              :disabled="busy"
              @input="onBorderColorInput"
            />
          </label>
        </section>
        <Transition name="custom-fields-pane" @after-enter="focusCustomFields">
          <div v-if="showCustomFields" class="card-type-fields">
            <CardTypeFieldsEditor ref="fieldsEditor" v-model="draftFields" :busy="busy" :errors="validationErrors" />
          </div>
        </Transition>
      </div>
    </template>

    <template #actions>
      <div v-if="draftName !== null" class="fixed-chrome-dialog-actions">
        <div class="card-type-dialog-leading-actions">
          <button
            v-if="showDeleteAction"
            type="button"
            class="btn btn--danger"
            :disabled="busy"
            aria-label="Delete card type"
            title="Delete card type"
            @click="deleteEditingCardType"
          >
            <Trash2 :size="16" aria-hidden="true" />
          </button>
        </div>
        <div class="fixed-chrome-dialog-actions-left">
          <button
            type="submit"
            class="btn"
            :disabled="busy || !hasValidName"
            :aria-label="isCreateMode ? 'Create card type' : 'Save card type'"
            :title="isCreateMode ? 'Create card type' : 'Save card type'"
          >
            <Check :size="16" aria-hidden="true" />
            <span>{{ isCreateMode ? 'Create' : 'Save' }}</span>
          </button>
          <button type="button" class="btn btn--secondary" :disabled="busy" aria-label="Cancel editing" title="Cancel" @click="closeDialog">
            <X :size="16" aria-hidden="true" />
            <span>Cancel</span>
          </button>
        </div>
      </div>
    </template>
  </FixedChromeDialog>
</template>

<script setup lang="ts">
import { Check, CircleAlert, TriangleAlert, Trash2, X } from '@lucide/vue';
import { storeToRefs } from 'pinia';
import { computed, nextTick, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import FixedChromeDialog from '../../shared/components/FixedChromeDialog.vue';
import EmojiPickerDropdown from '../../shared/components/EmojiPickerDropdown.vue';
import { useConfirm } from '../../shared/composables/useConfirm';
import type { CardTypeEditModel } from '../../shared/types/boardTypes';
import CardTypeFieldsEditor from './CardTypeFieldsEditor.vue';
import { useCardTypeFieldsPreview } from '../composables/useCardTypeFieldsPreview';
import type { AppError } from '../../shared/types/appError';
import { buildCardFieldDefinitions, createCardFieldDraft, fieldErrorIndex, isInlineFieldError, retainCardFieldErrors, type CardFieldDraft } from '../utils/cardFieldDraft';
import { useBoardStore } from '../stores/boardStore';
import { useCardTypeStore } from '../stores/cardTypeStore';
import {
  createCardTypeStyleDraft,
  DEFAULT_CARD_TYPE_STYLE_NAME,
  DEFAULT_CARD_TYPE_STYLE_PROPERTIES_JSON,
  getCardSurfaceClassList,
  getCardSurfaceStyle,
  normaliseCardTypeEmojiForRender
} from '../../shared/utils/cardTypeStyles';
import { PRESET_TOKENS } from '../../shared/utils/presetTheme';
import { parseStyleNameInput, useStyleDraft } from '../composables/useStyleDraft';

const route = useRoute();
const router = useRouter();
const boardStore = useBoardStore();
const cardTypeStore = useCardTypeStore();
const { confirm } = useConfirm();
const { currentBoardId } = storeToRefs(boardStore);
const { busy } = storeToRefs(cardTypeStore);
const { createCardType, updateCardType, deleteCardType, getCardTypeById } = cardTypeStore;

const draftName = ref<string | null>(null);
const draftEmoji = ref<string | null>(null);
const draftFields = ref<CardFieldDraft[]>([]);
const customFieldsOpened = ref(false);
const fieldsEditor = ref<InstanceType<typeof CardTypeFieldsEditor> | null>(null);
const editorError = ref<AppError | null>(null);
const errorSummary = ref<HTMLElement | null>(null);
const nameInput = ref<HTMLInputElement | null>(null);
const validationErrors = computed(() => ({ ...previewError.value?.validationErrors, ...editorError.value?.validationErrors }));
const summaryErrors = computed(() => Object.fromEntries(Object.entries(validationErrors.value)
  .filter(([path]) => path !== 'name' && !path.startsWith('fields') && !path.startsWith('cards['))));
const hasSummaryError = computed(() => editorError.value !== null
  && (Object.keys(validationErrors.value).length === 0 || Object.keys(summaryErrors.value).length > 0));
const {
  draft: draftStyle,
  stylePropertiesJson,
  setDraft: setDraftStyle,
  clearDraft: clearDraftStyle,
  setStyleName,
  setTextMode,
  setBorderMode,
  setField: setDraftStyleField
} = useStyleDraft();
const draftSourceKey = ref<string | null>(null);
const presetColours = PRESET_TOKENS;

const isCreateMode = computed(() => route.name === 'card-types-new');
const isDuplicateMode = computed(() => isCreateMode.value && route.query.duplicateFrom !== undefined);
const duplicateSource = computed(() => {
  const value = route.query.duplicateFrom;
  if (!isDuplicateMode.value || typeof value !== 'string' || !/^\d+$/.test(value)) return null;
  return getCardTypeById(Number(value));
});
const boardId = computed(() => currentBoardId.value!);
const routeCardTypeId = computed<number | null>(() => {
  const parsed = Number.parseInt(String(route.params.cardTypeId ?? ''), 10);
  return Number.isFinite(parsed) ? parsed : null;
});
const editingCardType = computed(() => getCardTypeById(routeCardTypeId.value));
const previewRequest = computed(() => {
  const type = editingCardType.value;
  if (isCreateMode.value || !type || draftSourceKey.value !== `edit:${boardId.value}:${type.id}`) return null;
  const fields = buildCardFieldDefinitions(draftFields.value);
  const original = buildCardFieldDefinitions(type.fields.map(createCardFieldDraft));
  if (JSON.stringify(fields) === JSON.stringify(original)) return null;
  return { boardId: boardId.value, cardTypeId: type.id, fields };
});
const { impacts: fieldImpacts, error: previewError } = useCardTypeFieldsPreview(() => previewRequest.value);
const showCustomFields = computed(() => customFieldsOpened.value || draftFields.value.length > 0 || (editingCardType.value?.fields.length ?? 0) > 0);
const dialogTitle = computed(() => {
  if (isDuplicateMode.value) {
    return `Duplicate Card Type: ${duplicateSource.value?.name ?? ''}`;
  }
  if (isCreateMode.value) {
    return 'Add Card Type';
  }

  if (editingCardType.value) {
    return `Edit Card Type: ${editingCardType.value.name}`;
  }

  return 'Edit Card Type';
});
const hasValidName = computed(() => (draftName.value ?? '').trim().length > 0);
const showDeleteAction = computed(() => !isCreateMode.value && editingCardType.value !== null && !editingCardType.value.isSystem);
const previewName = computed(() => {
  const value = (draftName.value ?? '').trim();
  if (value.length > 0) {
    return value;
  }

  return isCreateMode.value ? 'New card type' : (editingCardType.value?.name ?? 'Card type');
});
const previewEmoji = computed(() => normaliseCardTypeEmojiForRender(draftEmoji.value));
const previewTitle = computed(() => (previewEmoji.value ? `${previewEmoji.value} ${previewName.value}` : previewName.value));
const previewCardStyle = computed(() => {
  if (!draftStyle.value) {
    return getCardSurfaceStyle(editingCardType.value);
  }

  return getCardSurfaceStyle({
    styleName: draftStyle.value.styleName,
    stylePropertiesJson: stylePropertiesJson.value ?? DEFAULT_CARD_TYPE_STYLE_PROPERTIES_JSON
  });
});
const previewCardStyleClasses = computed(() => {
  if (!draftStyle.value) {
    return getCardSurfaceClassList(editingCardType.value);
  }

  return getCardSurfaceClassList({
    styleName: draftStyle.value.styleName,
    stylePropertiesJson: stylePropertiesJson.value ?? DEFAULT_CARD_TYPE_STYLE_PROPERTIES_JSON
  });
});

watch(
  [boardId, routeCardTypeId, isCreateMode, editingCardType, isDuplicateMode, duplicateSource],
  async ([nextBoardId, nextCardTypeId, nextIsCreate, nextCardType, nextIsDuplicate, nextDuplicateSource]) => {
    if (nextIsCreate) {
      if (nextIsDuplicate && !nextDuplicateSource) {
        clearDraft();
        await router.replace({ name: 'card-types', params: { boardId: nextBoardId } });
        return;
      }
      const sourceKey = `create:${nextBoardId}:${nextDuplicateSource?.id ?? 'new'}`;
      if (draftSourceKey.value === sourceKey) {
        return;
      }

      clearDraft();
      draftName.value = '';
      draftEmoji.value = nextDuplicateSource?.emoji ?? null;
      draftFields.value = (nextDuplicateSource?.fields ?? []).map(field => ({
        ...createCardFieldDraft(field), persisted: false, fieldCodeEdited: true
      }));
      setDraftStyle(createCardTypeStyleDraft(nextDuplicateSource ?? {
        styleName: DEFAULT_CARD_TYPE_STYLE_NAME,
        stylePropertiesJson: DEFAULT_CARD_TYPE_STYLE_PROPERTIES_JSON
      }));
      draftSourceKey.value = sourceKey;
      if (nextIsDuplicate) {
        await nextTick();
        nameInput.value?.focus();
      }
      return;
    }

    if (nextCardTypeId === null || !nextCardType) {
      clearDraft();
      await router.replace({ name: 'card-types', params: { boardId: nextBoardId } });
      return;
    }

    const sourceKey = `edit:${nextBoardId}:${nextCardTypeId}`;
    if (draftSourceKey.value === sourceKey) {
      return;
    }

    clearDraft();
    draftFields.value = nextCardType.fields.map(createCardFieldDraft);
    draftName.value = nextCardType.name;
    draftEmoji.value = nextCardType.emoji;
    setDraftStyle(createCardTypeStyleDraft(nextCardType));
    draftSourceKey.value = sourceKey;
  },
  { immediate: true }
);

async function closeDialog() {
  await router.push({ name: 'card-types', params: { boardId: boardId.value } });
}

async function saveCardType() {
  const targetBoardId = boardId.value;
  const canonicalName = (draftName.value ?? '').trim();
  if (!canonicalName || !draftStyle.value) {
    return;
  }

  const nextStylePropertiesJson = stylePropertiesJson.value;
  if (!nextStylePropertiesJson) {
    return;
  }

  const fields = buildCardFieldDefinitions(draftFields.value);

  const saveModel: CardTypeEditModel = {
    fields,
    name: canonicalName,
    emoji: draftEmoji.value,
    styleName: draftStyle.value.styleName,
    stylePropertiesJson: nextStylePropertiesJson
  };

  if (isCreateMode.value) {
    const created = await createCardType(saveModel, targetBoardId);
    if (!created) {
      await showSaveError(cardTypeStore.saveError);
      return;
    }

    await closeDialog();
    return;
  }

  if (!editingCardType.value) {
    return;
  }

  const updated = await updateCardType(editingCardType.value.id, saveModel, targetBoardId);
  if (!updated) {
    await showSaveError(cardTypeStore.saveError);
    return;
  }

  await closeDialog();
}

async function deleteEditingCardType() {
  const cardType = editingCardType.value;
  const targetBoardId = boardId.value;
  if (!cardType || cardType.isSystem) {
    return;
  }

  const confirmed = await confirm({
    title: 'Delete card type',
    message: `Delete card type "${cardType.name}"?\n\nCards using this type will be reassigned to the board default type.`,
    confirmLabel: 'Delete',
    danger: true
  });
  if (!confirmed || boardId.value !== targetBoardId) {
    return;
  }

  const deleted = await deleteCardType(cardType.id, targetBoardId);
  if (!deleted) {
    await showSaveError(cardTypeStore.saveError);
    return;
  }

  await closeDialog();
}

watch(() => ({
  name: draftName.value,
  emoji: draftEmoji.value,
  styleName: draftStyle.value?.styleName,
  stylePropertiesJson: stylePropertiesJson.value,
  fields: draftFields.value.map(field => ({ ...field }))
}), (current, previous) => {
  const error = editorError.value;
  if (!error) return;
  const errors = retainCardFieldErrors(error.validationErrors ?? {}, previous.fields, current.fields);
  for (const property of ['name', 'emoji', 'styleName', 'stylePropertiesJson'] as const) {
    if (current[property] !== previous[property]) delete errors[property];
  }
  editorError.value = Object.keys(errors).length ? { ...error, validationErrors: errors } : null;
});

async function showSaveError(error: AppError | null) {
  editorError.value = error;
  await nextTick();
  if (validationErrors.value.name) {
    nameInput.value?.focus();
    return;
  }
  const fieldPath = Object.keys(validationErrors.value).find(isInlineFieldError);
  if (fieldPath) {
    const index = fieldErrorIndex(fieldPath, draftFields.value);
    if (index !== null) await fieldsEditor.value?.revealField(index);
    return;
  }
  if (hasSummaryError.value) errorSummary.value?.focus();
  else fieldsEditor.value?.focusSummary();
}

function onStyleNameChange(event: Event) { setStyleName(parseStyleNameInput((event.target as HTMLSelectElement).value)); }
function onTextModeChange(event: Event) { setTextMode((event.target as HTMLSelectElement).value); }
function onBorderModeChange(event: Event) { setBorderMode((event.target as HTMLSelectElement).value); }
function onBackgroundColorInput(event: Event) { setDraftStyleField('backgroundColor', (event.target as HTMLInputElement).value); }
function onLeftColorInput(event: Event) { setDraftStyleField('leftColor', (event.target as HTMLInputElement).value); }
function onRightColorInput(event: Event) { setDraftStyleField('rightColor', (event.target as HTMLInputElement).value); }
function onTextColorInput(event: Event) { setDraftStyleField('textColor', (event.target as HTMLInputElement).value); }
function onBorderColorInput(event: Event) { setDraftStyleField('borderColor', (event.target as HTMLInputElement).value); }

function openCustomFields() {
  customFieldsOpened.value = true;
}

function focusCustomFields() {
  fieldsEditor.value?.focusAddButton();
}

function clearDraft() {
  customFieldsOpened.value = false;
  draftFields.value = [];
  editorError.value = null;
  cardTypeStore.clearSaveError();
  draftName.value = null;
  draftEmoji.value = null;
  clearDraftStyle();
  draftSourceKey.value = null;
}
</script>

<style scoped>
.card-type-dialog {
  width: min(34rem, calc(100vw - 2rem));
  transition: width 220ms ease;
}

.card-type-dialog--fields {
  width: min(64rem, calc(100vw - 2rem));
}

.card-type-dialog--fields :deep(.fixed-chrome-dialog__surface) {
  height: min(48rem, calc(100dvh - 2rem));
}

.card-type-dialog--fields :deep(.fixed-chrome-dialog__body) {
  display: flex;
  flex-direction: column;
}

.card-type-preview {
  display: flex;
  align-items: flex-start;
  gap: 0.5rem;
  flex-shrink: 0;
  max-height: 9rem;
  overflow-y: auto;
  margin-bottom: 0.75rem;
  padding: 0.65rem 0.75rem;
  border: 1px solid var(--bo-toast-warning-border);
  border-radius: 0.5rem;
  background: var(--bo-toast-warning-bg);
  color: var(--bo-toast-warning-text);
  font-size: 0.875rem;
}

.card-type-preview > svg { flex-shrink: 0; margin-top: 0.15rem; }
.card-type-preview > div { min-width: 0; overflow-wrap: anywhere; }
.card-type-preview p { margin: 0; }
.card-type-preview p + p { margin-top: 0.25rem; }
.card-type-preview-error { font-size: 0.875rem; }

.card-type-columns {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(0, 0fr);
  min-height: 0;
  gap: 0;
  transition: grid-template-columns 220ms ease, gap 220ms ease;
}

.card-type-dialog--fields .card-type-columns {
  flex: 1;
  gap: 1.5rem;
  grid-template-columns: minmax(0, 0.85fr) minmax(0, 1.15fr);
}

.card-type-section-heading {
  margin: 0 0 0.25rem;
  font-size: 1rem;
}

.card-type-appearance {
  display: grid;
  align-content: start;
  gap: 0.5rem;
  min-width: 0;
  min-height: 0;
  padding: 2px;
}

.card-type-dialog--fields .card-type-appearance {
  overflow-y: auto;
  overscroll-behavior: contain;
}

.card-type-appearance-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
}

.card-type-appearance-header .card-type-section-heading {
  margin: 0;
}

.card-type-fields-toggle {
  margin-left: auto;
}

.card-type-fields {
  min-width: 0;
  min-height: 0;
  padding-left: 1.5rem;
  border-left: 1px solid var(--bo-border-soft);
}

.custom-fields-pane-enter-active,
.custom-fields-pane-leave-active {
  overflow: hidden;
  transition: opacity 220ms ease, transform 220ms ease;
}

.custom-fields-pane-enter-from,
.custom-fields-pane-leave-to {
  opacity: 0;
  transform: translateX(0.75rem);
}

@media (max-width: 800px) {
  .card-type-columns {
    grid-template-columns: minmax(0, 1fr);
    grid-template-rows: minmax(0, 1fr) minmax(0, 0fr);
    transition: grid-template-rows 220ms ease, gap 220ms ease;
  }

  .custom-fields-pane-enter-from,
  .custom-fields-pane-leave-to {
    transform: translateY(0.75rem);
  }

  .card-type-dialog--fields .card-type-columns {
    grid-template-columns: minmax(0, 1fr);
    grid-template-rows: minmax(0, 0.8fr) minmax(0, 1.2fr);
    gap: 1rem;
  }

  .card-type-fields {
    padding: 1rem 0 0;
    border-left: 0;
    border-top: 1px solid var(--bo-border-soft);
  }
}

@media (max-width: 720px) {
  .card-type-dialog {
    width: 100vw;
  }

  .card-type-dialog--fields :deep(.fixed-chrome-dialog__surface) {
    height: 100%;
  }
}

.card-type-name-control {
  display: grid;
  gap: 0.2rem;
}

.card-type-name-control .card-type-inline-error {
  display: flex;
  align-items: flex-start;
  gap: 0.25rem;
  margin: 0;
  color: var(--bo-colour-danger-ink);
  font-size: 0.75rem;
  font-weight: 500;
  line-height: 1.25;
}

.card-type-errors {
  flex-shrink: 0;
  max-height: 20dvh;
  overflow-y: auto;
  margin: 0;
  font-size: 0.85rem;
}

.card-type-inline-error > svg, .card-type-errors p > svg {
  flex-shrink: 0;
  margin-top: 0.1em;
}

.card-type-errors p {
  display: flex;
  align-items: flex-start;
  gap: 0.25rem;
  margin: 0 0 0.3rem;
}

input[aria-invalid="true"] {
  border-color: var(--bo-colour-danger);
}

.card-types-dialog-preview {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
}

.card-type-preview-card {
  flex: 1;
  min-width: 0;
  border: 1px solid var(--bo-card-surface-border-color, var(--bo-border-soft));
  border-radius: 12px;
  padding: 0.6rem;
  background: var(--bo-card-surface-background, var(--bo-surface-base));
  color: var(--bo-card-surface-color, inherit);
}

.card-types-emoji-picker-wrap {
  margin-top: 0.25rem;
}

.card-types-colour-input {
  min-height: 2.25rem;
  padding: 0.2rem;
}

.card-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 0.5rem;
}

.card-type-dialog-leading-actions {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
}

.card-title {
  min-width: 0;
  line-height: 1.25;
  overflow-wrap: anywhere;
}

.card-id {
  flex: 0 0 auto;
  font-weight: 600;
  line-height: 1.25;
}

.card-types-preset-picker {
  display: flex;
  flex-wrap: wrap;
  gap: 0.4rem;
}

.card-types-preset-swatch {
  width: 1.7rem;
  height: 1.7rem;
  border-radius: 999px;
  border: 1px solid var(--bo-border-default);
  cursor: pointer;
  padding: 0;
}

.card-types-preset-swatch--selected {
  outline: 2px solid var(--bo-focus-ring);
  outline-offset: 1px;
}
@media (prefers-reduced-motion: reduce) {
  .card-type-dialog,
  .card-type-columns,
  .custom-fields-pane-enter-active,
  .custom-fields-pane-leave-active {
    transition: none;
  }
}
</style>
