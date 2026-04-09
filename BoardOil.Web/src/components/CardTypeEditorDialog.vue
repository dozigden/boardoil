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
        <article class="card-type-preview-card" :style="previewCardStyle">
          <div class="card-header">
            <strong>{{ previewTitle }}</strong>
            <span class="badge">#123</span>
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
          @input="draftName = ($event.target as HTMLInputElement).value"
        />
      </label>

      <label>
        Emoji
        <div class="card-types-emoji-picker-wrap">
          <EmojiPickerDropdown v-model="draftEmoji" :disabled="busy" placeholder="Select emoji" />
        </div>
      </label>

      <label>
        Style
        <select :value="draftStyle.styleName" :disabled="busy" @change="setStyleName(($event.target as HTMLSelectElement).value)">
          <option value="solid">Solid</option>
          <option value="gradient">Gradient</option>
        </select>
      </label>

      <template v-if="draftStyle.styleName === 'solid'">
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

      <template v-else>
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

      <label>
        Text Color Mode
        <select :value="draftStyle.textColorMode" :disabled="busy" @change="setTextMode(($event.target as HTMLSelectElement).value)">
          <option value="auto">Auto Contrast</option>
          <option value="custom">Custom</option>
        </select>
      </label>

      <label v-if="draftStyle.textColorMode === 'custom'">
        Text Color
        <input
          type="color"
          class="card-types-colour-input"
          :value="draftStyle.textColor"
          :disabled="busy"
          @input="setDraftStyleField('textColor', ($event.target as HTMLInputElement).value)"
        />
      </label>

      <label>
        Border
        <select :value="draftStyle.borderMode" :disabled="busy" @change="setBorderMode(($event.target as HTMLSelectElement).value)">
          <option value="auto">Auto</option>
          <option value="custom">Custom</option>
          <option value="none">None</option>
        </select>
      </label>

      <label v-if="draftStyle.borderMode === 'custom'">
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
        <span v-else />
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
import ModalDialog from './ModalDialog.vue';
import EmojiPickerDropdown from './EmojiPickerDropdown.vue';
import { useCardTypeStore } from '../stores/cardTypeStore';
import {
  createCardTypeStyleDraft,
  DEFAULT_CARD_TYPE_STYLE_NAME,
  DEFAULT_CARD_TYPE_STYLE_PROPERTIES_JSON,
  getCardSurfaceStyle,
  normaliseCardTypeEmojiForRender
} from '../utils/cardTypeStyles';
import { useStyleDraft } from '../composables/useStyleDraft';

const route = useRoute();
const router = useRouter();
const cardTypeStore = useCardTypeStore();
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

  const confirmed = window.confirm(
    `Delete card type "${editingCardType.value.name}"?\n\nCards using this type will be reassigned to the board system type.`
  );
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
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
}
</style>
