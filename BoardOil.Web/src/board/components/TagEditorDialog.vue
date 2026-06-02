<template>
  <ModalDialog
    :open="isCreateMode || editingTag !== null"
    :title="dialogTitle"
    :close-label="isCreateMode ? 'Cancel creating' : 'Cancel editing'"
    @close="closeTagEditor"
    @submit="saveTag"
  >
    <template v-if="draft">
      <div class="tags-dialog-preview">
        <span class="badge">Preview</span>
        <span
          class="tag"
          :class="[previewStyleClasses, { 'tag--with-emoji': previewEmoji }]"
          :style="previewStyle"
          :aria-label="previewTagName"
        >
          <span v-if="previewEmoji" class="tag-emoji" aria-hidden="true">{{ previewEmoji }}</span>
          {{ previewTagName }}
        </span>
      </div>

      <label>
        Name
        <input
          :value="draftTagName"
          maxlength="40"
          :placeholder="isCreateMode ? 'New tag name' : 'Tag name'"
          :disabled="busy"
          autocomplete="off"
          data-lpignore="true"
          @input="setDraftTagName(($event.target as HTMLInputElement).value)"
        />
      </label>

      <label>
        Emoji
        <div class="tags-emoji-picker-wrap">
          <EmojiPickerDropdown v-model="draftEmoji" :disabled="busy" :teleport="false" placeholder="Select emoji" />
        </div>
      </label>

      <label>
        Style
        <select :value="draft.styleName" :disabled="busy" @change="setStyleName(parseStyleNameInput(($event.target as HTMLSelectElement).value))">
          <option value="auto">Auto</option>
          <option value="presets">Presets</option>
          <option value="solid">Solid</option>
          <option value="gradient">Gradient</option>
        </select>
      </label>

      <template v-if="draft.styleName === 'presets'">
        <label>
          Preset
          <div class="tags-preset-picker" role="radiogroup" aria-label="Tag preset colour">
            <button
              v-for="preset in presetColours"
              :key="preset.cssVar"
              type="button"
              class="tags-preset-swatch"
              :class="{ 'tags-preset-swatch--selected': draft.presetIndex === preset.index }"
              :style="{ backgroundColor: preset.cssValue }"
              :disabled="busy"
              :aria-pressed="draft.presetIndex === preset.index"
              :aria-label="`Preset ${preset.index + 1}`"
              @click="setDraftField('presetIndex', preset.index)"
            />
          </div>
        </label>
      </template>

      <template v-else-if="draft.styleName === 'solid'">
        <label>
          Background Color
          <input
            type="color"
            class="tags-colour-input"
            :value="draft.backgroundColor"
            :disabled="busy"
            @input="setDraftField('backgroundColor', ($event.target as HTMLInputElement).value)"
          />
        </label>
      </template>

      <template v-else-if="draft.styleName === 'gradient'">
        <label>
          Left Color
          <input
            type="color"
            class="tags-colour-input"
            :value="draft.leftColor"
            :disabled="busy"
            @input="setDraftField('leftColor', ($event.target as HTMLInputElement).value)"
          />
        </label>
        <label>
          Right Color
          <input
            type="color"
            class="tags-colour-input"
            :value="draft.rightColor"
            :disabled="busy"
            @input="setDraftField('rightColor', ($event.target as HTMLInputElement).value)"
          />
        </label>
      </template>

      <label v-if="draft.styleName === 'solid' || draft.styleName === 'gradient'">
        Text Color Mode
        <select :value="draft.textColorMode" :disabled="busy" @change="setTextMode(($event.target as HTMLSelectElement).value)">
          <option value="auto">Auto Contrast</option>
          <option value="custom">Custom</option>
        </select>
      </label>

      <label v-if="(draft.styleName === 'solid' || draft.styleName === 'gradient') && draft.textColorMode === 'custom'">
        Text Color
        <input
          type="color"
          class="tags-colour-input"
          :value="draft.textColor"
          :disabled="busy"
          @input="setDraftField('textColor', ($event.target as HTMLInputElement).value)"
        />
      </label>

      <label v-if="draft.styleName === 'solid' || draft.styleName === 'gradient'">
        Border
        <select :value="draft.borderMode" :disabled="busy" @change="setBorderMode(($event.target as HTMLSelectElement).value)">
          <option value="auto">Auto</option>
          <option value="custom">Custom</option>
          <option value="none">None</option>
        </select>
      </label>

      <label v-if="(draft.styleName === 'solid' || draft.styleName === 'gradient') && draft.borderMode === 'custom'">
        Border Color
        <input
          type="color"
          class="tags-colour-input"
          :value="draft.borderColor"
          :disabled="busy"
          @input="setDraftField('borderColor', ($event.target as HTMLInputElement).value)"
        />
      </label>
    </template>

    <template #actions>
      <div v-if="draft" class="editor-actions card-modal-actions">
        <button
          v-if="!isCreateMode && editingTag"
          type="button"
          class="btn btn--danger"
          :disabled="busy"
          aria-label="Delete tag"
          title="Delete tag"
          @click="deleteEditingTag"
        >
          <Trash2 :size="16" aria-hidden="true" />
        </button>
        <span v-else />
        <div class="card-modal-actions-left">
          <button type="submit" class="btn" :disabled="busy || !hasValidDraftTagName" :aria-label="saveButtonAriaLabel" :title="saveButtonAriaLabel">
            <Check :size="16" aria-hidden="true" />
            <span>{{ saveButtonLabel }}</span>
          </button>
          <button type="button" class="btn btn--secondary" :disabled="busy" aria-label="Cancel editing" title="Cancel" @click="closeTagEditor">
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
import { useCardStore } from '../stores/cardStore';
import { useBoardStore } from '../stores/boardStore';
import { useTagStore } from '../stores/tagStore';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import type { Tag, TagEditModel } from '../../shared/types/boardTypes';
import {
  DEFAULT_TAG_STYLE_PROPERTIES_JSON,
  getTagPillClassList,
  createTagStyleDraft,
  getTagPillStyle,
  normaliseTagEmojiForRender
} from '../../shared/utils/tagStyles';
import { PRESET_TOKENS } from '../../shared/utils/presetTheme';
import { parseStyleNameInput, useStyleDraft } from '../composables/useStyleDraft';
import EmojiPickerDropdown from '../../shared/components/EmojiPickerDropdown.vue';
import ModalDialog from '../../shared/components/ModalDialog.vue';
import { useConfirm } from '../../shared/composables/useConfirm';

const route = useRoute();
const router = useRouter();
const cardStore = useCardStore();
const boardStore = useBoardStore();
const tagStore = useTagStore();
const { confirm } = useConfirm();
const feedbackStore = useUiFeedbackStore();
const { currentBoardId } = storeToRefs(boardStore);
const { busy } = storeToRefs(tagStore);
const { saveTag: saveTagAction, deleteTag, getTagById, getTagByName, loadTags } = tagStore;
const {
  draft,
  stylePropertiesJson,
  setDraft,
  clearDraft,
  setStyleName,
  setTextMode,
  setBorderMode,
  setField: setDraftField
} = useStyleDraft();
const draftEmoji = ref<string | null>(null);
const draftTagName = ref('');
const draftSourceKey = ref<string | null>(null);
const presetColours = PRESET_TOKENS;

const isCreateMode = computed(() => route.name === 'tags-new');
const routeTagId = computed<number | null>(() => {
  const rawTagId = route.params.tagId;
  const parsedTagId = typeof rawTagId === 'string'
    ? Number.parseInt(rawTagId, 10)
    : Number.NaN;
  if (!Number.isFinite(parsedTagId)) {
    return null;
  }

  return parsedTagId;
});

const boardId = computed(() => currentBoardId.value!);

const editingTag = computed(() => getTagById(routeTagId.value));
const dialogTitle = computed(() => {
  if (isCreateMode.value) {
    return 'Add Tag';
  }

  if (editingTag.value) {
    return `Edit Tag: ${editingTag.value.name}`;
  }

  return 'Edit Tag';
});
const previewTagName = computed(() => {
  const value = draftTagName.value.trim();
  if (value.length > 0) {
    return value;
  }

  if (isCreateMode.value) {
    return 'New tag';
  }

  return editingTag.value?.name ?? 'Tag';
});
const hasValidDraftTagName = computed(() => draftTagName.value.trim().length > 0);
const saveButtonLabel = computed(() => (isCreateMode.value ? 'Create' : 'Save'));
const saveButtonAriaLabel = computed(() => (isCreateMode.value ? 'Create tag' : 'Save tag style'));
const previewStyle = computed(() => {
  if (!draft.value) {
    return getTagPillStyle(editingTag.value);
  }

  const sourceTag = editingTag.value;
  const previewTag: Tag = {
    id: sourceTag?.id ?? 0,
    name: previewTagName.value,
    styleName: draft.value.styleName,
    stylePropertiesJson: stylePropertiesJson.value ?? DEFAULT_TAG_STYLE_PROPERTIES_JSON,
    emoji: normaliseTagEmojiForRender(draftEmoji.value),
    createdAtUtc: sourceTag?.createdAtUtc ?? '1970-01-01T00:00:00Z',
    updatedAtUtc: sourceTag?.updatedAtUtc ?? '1970-01-01T00:00:00Z'
  };

  return getTagPillStyle(previewTag);
});
const previewStyleClasses = computed(() => {
  if (!draft.value) {
    return getTagPillClassList(editingTag.value);
  }

  const sourceTag = editingTag.value;
  const previewTag: Tag = {
    id: sourceTag?.id ?? 0,
    name: previewTagName.value,
    styleName: draft.value.styleName,
    stylePropertiesJson: stylePropertiesJson.value ?? DEFAULT_TAG_STYLE_PROPERTIES_JSON,
    emoji: normaliseTagEmojiForRender(draftEmoji.value),
    createdAtUtc: sourceTag?.createdAtUtc ?? '1970-01-01T00:00:00Z',
    updatedAtUtc: sourceTag?.updatedAtUtc ?? '1970-01-01T00:00:00Z'
  };

  return getTagPillClassList(previewTag);
});
const previewEmoji = computed(() => normaliseTagEmojiForRender(draftEmoji.value));

async function closeTagEditor() {
  await router.push({ name: 'tags', params: { boardId: boardId.value } });
}

function setDraftTagName(value: string) {
  draftTagName.value = value;
}

async function saveTag() {
  if (!draft.value) {
    return;
  }

  const nextStylePropertiesJson = stylePropertiesJson.value;
  if (!nextStylePropertiesJson) {
    return;
  }

  const canonicalTagName = draftTagName.value.trim();
  if (!canonicalTagName) {
    return;
  }

  const saveModel: TagEditModel = {
    name: canonicalTagName,
    emoji: draftEmoji.value,
    styleName: draft.value.styleName,
    stylePropertiesJson: nextStylePropertiesJson
  };

  if (isCreateMode.value) {
    const existingTag = getTagByName(canonicalTagName);
    if (existingTag) {
      feedbackStore.setError(`Tag '${existingTag.name}' already exists.`);
      return;
    }

    const saveResult = await saveTagAction(boardId.value, null, saveModel);
    if (!saveResult) {
      return;
    }

    if (!saveResult.savedTag && saveResult.createdTag) {
      await router.replace({ name: 'tags-tag', params: { boardId: boardId.value, tagId: saveResult.createdTag.id } });
      return;
    }

    await closeTagEditor();
    return;
  }

  if (!editingTag.value) {
    return;
  }

  const saveResult = await saveTagAction(boardId.value, editingTag.value.id, saveModel);
  if (!saveResult?.savedTag) {
    return;
  }

  await closeTagEditor();
}

async function deleteEditingTag() {
  if (!editingTag.value) {
    return;
  }

  const confirmed = await confirm({
    title: 'Delete tag',
    message: `Delete tag "${editingTag.value.name}"?\n\nThis removes the tag from all cards and cannot be undone.`,
    confirmLabel: 'Delete',
    danger: true
  });
  if (!confirmed) {
    return;
  }

  const deleted = await deleteTag(boardId.value, editingTag.value.id);
  if (!deleted) {
    return;
  }

  cardStore.removeTagFromCards(editingTag.value.name);
  await closeTagEditor();
}

function clearDraftState() {
  clearDraft();
  draftEmoji.value = null;
  draftTagName.value = '';
  draftSourceKey.value = null;
}

function initialiseCreateDraftState() {
  if (draftSourceKey.value === 'create' && draft.value !== null) {
    return;
  }

  const randomPresetIndex = Math.floor(Math.random() * presetColours.length);
  setDraft(createTagStyleDraft({
    styleName: 'presets',
    stylePropertiesJson: JSON.stringify({
      presetIndex: randomPresetIndex,
      textColorMode: 'auto'
    }),
    emoji: null
  }));
  draftEmoji.value = null;
  draftTagName.value = '';
  draftSourceKey.value = 'create';
}

function initialiseEditDraftState(tag: Tag, tagId: number) {
  const sourceKey = `edit:${tagId}`;
  if (draftSourceKey.value === sourceKey && draft.value !== null) {
    return;
  }

  setDraft(createTagStyleDraft(tag));
  draftEmoji.value = tag.emoji ?? null;
  draftTagName.value = tag.name;
  draftSourceKey.value = sourceKey;
}

watch(
  [boardId, routeTagId, isCreateMode, editingTag],
  async ([nextBoardId, nextTagId, nextIsCreate, nextTag]) => {
    if (nextIsCreate) {
      initialiseCreateDraftState();
      return;
    }

    if (nextTagId === null) {
      clearDraftState();
      await router.replace({ name: 'tags', params: { boardId: nextBoardId } });
      return;
    }

    if (!nextTag && (tagStore.activeBoardId !== nextBoardId || tagStore.tags.length === 0)) {
      const loaded = await loadTags(nextBoardId);
      if (!loaded) {
        return;
      }

      nextTag = getTagById(nextTagId);
    }

    if (!nextTag) {
      clearDraftState();
      await router.replace({ name: 'tags', params: { boardId: nextBoardId } });
      return;
    }

    initialiseEditDraftState(nextTag, nextTagId);
  },
  { immediate: true }
);
</script>

<style scoped>
.tags-dialog-preview {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
}

.tags-colour-input {
  min-height: 2.25rem;
  padding: 0.2rem;
}

.tags-emoji-picker-wrap {
  margin-top: 0.3rem;
}

.tags-preset-picker {
  display: flex;
  flex-wrap: wrap;
  gap: 0.4rem;
}

.tags-preset-swatch {
  width: 1.7rem;
  height: 1.7rem;
  border-radius: 999px;
  border: 1px solid var(--bo-border-default);
  cursor: pointer;
  padding: 0;
}

.tags-preset-swatch--selected {
  outline: 2px solid var(--bo-focus-ring);
  outline-offset: 1px;
}
</style>
