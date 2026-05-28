<template>
  <ModalDialog
    :open="isCreateMode || editingCardType !== null"
    :title="dialogTitle"
    :close-label="isCreateMode ? 'Cancel creating' : 'Cancel editing'"
    @close="closeDialog"
    @submit="saveCardType"
  >
    <template v-if="draftName !== null && draftStyle !== null">
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
        Name
        <input
          :value="draftName"
          maxlength="40"
          :placeholder="isCreateMode ? 'New card type name' : 'Card type name'"
          :disabled="busy"
          autocomplete="off"
          data-lpignore="true"
          @input="draftName = ($event.target as HTMLInputElement).value"
        />
      </label>

      <label>
        Emoji
        <div class="card-types-emoji-picker-wrap">
          <EmojiPickerDropdown v-model="draftEmoji" :disabled="busy" :teleport="false" placeholder="Select emoji" />
        </div>
      </label>

      <label>
        Style
        <select :value="draftStyle.styleName" :disabled="busy" @change="setStyleName(parseStyleNameInput(($event.target as HTMLSelectElement).value))">
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
            @input="setDraftStyleField('backgroundColor', ($event.target as HTMLInputElement).value)"
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
            @input="setDraftStyleField('leftColor', ($event.target as HTMLInputElement).value)"
          />
        </label>
        <label>
          Right Color
          <input
            type="color"
            class="card-types-colour-input"
            :value="draftStyle.rightColor"
            :disabled="busy"
            @input="setDraftStyleField('rightColor', ($event.target as HTMLInputElement).value)"
          />
        </label>
      </template>

      <label v-if="draftStyle.styleName === 'solid' || draftStyle.styleName === 'gradient'">
        Text Color Mode
        <select :value="draftStyle.textColorMode" :disabled="busy" @change="setTextMode(($event.target as HTMLSelectElement).value)">
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
          @input="setDraftStyleField('textColor', ($event.target as HTMLInputElement).value)"
        />
      </label>

      <label v-if="draftStyle.styleName === 'solid' || draftStyle.styleName === 'gradient'">
        Border
        <select :value="draftStyle.borderMode" :disabled="busy" @change="setBorderMode(($event.target as HTMLSelectElement).value)">
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
          @input="setDraftStyleField('borderColor', ($event.target as HTMLInputElement).value)"
        />
      </label>
    </template>

    <template #actions>
      <div v-if="draftName !== null" class="editor-actions card-modal-actions">
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
        <div class="card-modal-actions-left">
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
  </ModalDialog>
</template>

<script setup lang="ts">
import { Check, Trash2, X } from 'lucide-vue-next';
import { storeToRefs } from 'pinia';
import { computed, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import ModalDialog from '../../shared/components/ModalDialog.vue';
import EmojiPickerDropdown from '../../shared/components/EmojiPickerDropdown.vue';
import { useConfirm } from '../../shared/composables/useConfirm';
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
const cardTypeStore = useCardTypeStore();
const { confirm } = useConfirm();
const { busy } = storeToRefs(cardTypeStore);
const { createCardType, updateCardType, deleteCardType, getCardTypeById, loadCardTypes } = cardTypeStore;

const draftName = ref<string | null>(null);
const draftEmoji = ref<string | null>(null);
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
const routeBoardId = computed<number | null>(() => {
  const parsed = Number.parseInt(String(route.params.boardId ?? ''), 10);
  return Number.isFinite(parsed) ? parsed : null;
});
const routeCardTypeId = computed<number | null>(() => {
  const parsed = Number.parseInt(String(route.params.cardTypeId ?? ''), 10);
  return Number.isFinite(parsed) ? parsed : null;
});
const editingCardType = computed(() => getCardTypeById(routeCardTypeId.value));
const dialogTitle = computed(() => {
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
  [routeBoardId, routeCardTypeId, isCreateMode],
  async ([nextBoardId, nextCardTypeId, nextIsCreate]) => {
    if (nextBoardId === null) {
      clearDraft();
      await router.replace({ name: 'boards' });
      return;
    }

    if (nextIsCreate) {
      if (draftSourceKey.value === 'create') {
        return;
      }

      draftName.value = '';
      draftEmoji.value = null;
      setDraftStyle(createCardTypeStyleDraft({
        styleName: DEFAULT_CARD_TYPE_STYLE_NAME,
        stylePropertiesJson: DEFAULT_CARD_TYPE_STYLE_PROPERTIES_JSON
      }));
      draftSourceKey.value = 'create';
      return;
    }

    if (nextCardTypeId === null) {
      clearDraft();
      await router.replace({ name: 'card-types', params: { boardId: nextBoardId } });
      return;
    }

    let nextCardType = getCardTypeById(nextCardTypeId);
    if (!nextCardType) {
      const loaded = await loadCardTypes(nextBoardId);
      if (!loaded) {
        return;
      }

      nextCardType = getCardTypeById(nextCardTypeId);
    }

    if (!nextCardType) {
      clearDraft();
      await router.replace({ name: 'card-types', params: { boardId: nextBoardId } });
      return;
    }

    const sourceKey = `edit:${nextCardTypeId}`;
    if (draftSourceKey.value === sourceKey) {
      return;
    }

    draftName.value = nextCardType.name;
    draftEmoji.value = nextCardType.emoji;
    setDraftStyle(createCardTypeStyleDraft(nextCardType));
    draftSourceKey.value = sourceKey;
  },
  { immediate: true }
);

async function closeDialog() {
  const boardId = routeBoardId.value;
  if (boardId === null) {
    await router.push({ name: 'boards' });
    return;
  }

  await router.push({ name: 'card-types', params: { boardId } });
}

async function saveCardType() {
  const boardId = routeBoardId.value;
  const canonicalName = (draftName.value ?? '').trim();
  if (boardId === null || !canonicalName || !draftStyle.value) {
    return;
  }

  const nextStylePropertiesJson = stylePropertiesJson.value;
  if (!nextStylePropertiesJson) {
    return;
  }

  if (isCreateMode.value) {
    const created = await createCardType(
      canonicalName,
      draftEmoji.value,
      draftStyle.value.styleName,
      nextStylePropertiesJson,
      boardId
    );
    if (!created) {
      return;
    }

    await closeDialog();
    return;
  }

  if (!editingCardType.value) {
    return;
  }

  const updated = await updateCardType(
    editingCardType.value.id,
    canonicalName,
    draftEmoji.value,
    draftStyle.value.styleName,
    nextStylePropertiesJson,
    boardId
  );
  if (!updated) {
    return;
  }

  await closeDialog();
}

async function deleteEditingCardType() {
  if (!editingCardType.value || routeBoardId.value === null || editingCardType.value.isSystem) {
    return;
  }

  const confirmed = await confirm({
    title: 'Delete card type',
    message: `Delete card type "${editingCardType.value.name}"?\n\nCards using this type will be reassigned to the board default type.`,
    confirmLabel: 'Delete',
    danger: true
  });
  if (!confirmed) {
    return;
  }

  const deleted = await deleteCardType(editingCardType.value.id, routeBoardId.value);
  if (!deleted) {
    return;
  }

  await closeDialog();
}

function clearDraft() {
  draftName.value = null;
  draftEmoji.value = null;
  clearDraftStyle();
  draftSourceKey.value = null;
}
</script>

<style scoped>
.card-types-dialog-preview {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
}

.card-type-preview-card {
  min-width: 15rem;
  border: 1px solid var(--bo-border-soft);
  border-radius: 12px;
  padding: 0.6rem;
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
</style>
