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
        <span class="tag" :class="{ 'tag--with-emoji': previewEmoji }" :style="previewStyle" :aria-label="previewTagName">
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
          @input="setDraftTagName(($event.target as HTMLInputElement).value)"
        />
      </label>

      <label>
        Emoji
        <div class="tags-emoji-picker-wrap">
          <EmojiPickerDropdown v-model="draftEmoji" :disabled="busy" placeholder="Select emoji" />
        </div>
      </label>

      <label>
        Style
        <select :value="draft.styleName" :disabled="busy" @change="setStyleName(($event.target as HTMLSelectElement).value)">
          <option value="solid">Solid</option>
          <option value="gradient">Gradient</option>
        </select>
      </label>

      <template v-if="draft.styleName === 'solid'">
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

      <template v-else>
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

      <label>
        Text Color Mode
        <select :value="draft.textColorMode" :disabled="busy" @change="setTextMode(($event.target as HTMLSelectElement).value)">
          <option value="auto">Auto Contrast</option>
          <option value="custom">Custom</option>
        </select>
      </label>

      <label v-if="draft.textColorMode === 'custom'">
        Text Color
        <input
          type="color"
          class="tags-colour-input"
          :value="draft.textColor"
          :disabled="busy"
          @input="setDraftField('textColor', ($event.target as HTMLInputElement).value)"
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
import { useTagStore } from '../stores/tagStore';
import { useUiFeedbackStore } from '../stores/uiFeedbackStore';
import type { Tag, TagStyleName } from '../types/boardTypes';
import {
  DEFAULT_TAG_STYLE_PROPERTIES_JSON,
  buildStylePropertiesJsonFromDraft,
  createTagStyleDraft,
  getTagPillStyle,
  normaliseTagEmojiForRender
} from '../utils/tagStyles';
import type { TagStyleDraft } from '../utils/tagStyles';
import EmojiPickerDropdown from './EmojiPickerDropdown.vue';
import ModalDialog from './ModalDialog.vue';

const route = useRoute();
const router = useRouter();
const cardStore = useCardStore();
const tagStore = useTagStore();
const feedbackStore = useUiFeedbackStore();
const { busy } = storeToRefs(tagStore);
const { createTag, updateTagStyle, deleteTag, getTagById, getTagByName, loadTags } = tagStore;
const draft = ref<TagStyleDraft | null>(null);
const draftEmoji = ref<string | null>(null);
const draftTagName = ref('');
const draftSourceKey = ref<string | null>(null);

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

const routeBoardId = computed<number | null>(() => {
  const rawBoardId = route.params.boardId;
  const parsedBoardId = typeof rawBoardId === 'string'
    ? Number.parseInt(rawBoardId, 10)
    : Number.NaN;
  if (!Number.isFinite(parsedBoardId)) {
    return null;
  }

  return parsedBoardId;
});

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
    stylePropertiesJson: buildStylePropertiesJsonFromDraft(draft.value),
    emoji: normaliseTagEmojiForRender(draftEmoji.value),
    createdAtUtc: sourceTag?.createdAtUtc ?? '1970-01-01T00:00:00Z',
    updatedAtUtc: sourceTag?.updatedAtUtc ?? '1970-01-01T00:00:00Z'
  };

  return getTagPillStyle(previewTag);
});
const previewEmoji = computed(() => normaliseTagEmojiForRender(draftEmoji.value));

async function closeTagEditor() {
  const boardId = routeBoardId.value;
  if (boardId === null) {
    await router.push({ name: 'boards' });
    return;
  }

  await router.push({ name: 'tags', params: { boardId } });
}

function setDraftTagName(value: string) {
  draftTagName.value = value;
}

function setStyleName(value: string) {
  if (!draft.value) {
    return;
  }

  const styleName: TagStyleName = value === 'gradient' ? 'gradient' : 'solid';
  draft.value = {
    ...draft.value,
    styleName
  };
}

function setTextMode(value: string) {
  if (!draft.value) {
    return;
  }

  draft.value = {
    ...draft.value,
    textColorMode: value === 'custom' ? 'custom' : 'auto'
  };
}

function setDraftField(field: keyof TagStyleDraft, value: string) {
  if (!draft.value) {
    return;
  }

  draft.value = {
    ...draft.value,
    [field]: value
  };
}

async function saveTag() {
  if (!draft.value || routeBoardId.value === null) {
    return;
  }

  const stylePropertiesJson = buildStylePropertiesJsonFromDraft(draft.value);
  const canonicalTagName = draftTagName.value.trim();
  if (isCreateMode.value) {
    if (!canonicalTagName) {
      return;
    }

    const existingTag = getTagByName(canonicalTagName);
    if (existingTag) {
      feedbackStore.setError(`Tag '${existingTag.name}' already exists.`);
      return;
    }

    const createdTag = await createTag(canonicalTagName, routeBoardId.value, draftEmoji.value);
    if (!createdTag) {
      return;
    }

    const styledTag = await updateTagStyle(
      createdTag.id,
      canonicalTagName,
      draft.value.styleName,
      stylePropertiesJson,
      draftEmoji.value,
      routeBoardId.value
    );
    if (!styledTag) {
      await router.replace({ name: 'tags-tag', params: { boardId: routeBoardId.value, tagId: createdTag.id } });
      return;
    }

    await closeTagEditor();
    return;
  }

  if (!editingTag.value) {
    return;
  }

  const updatedTag = await updateTagStyle(
    editingTag.value.id,
    canonicalTagName,
    draft.value.styleName,
    stylePropertiesJson,
    draftEmoji.value,
    routeBoardId.value
  );
  if (!updatedTag) {
    return;
  }

  await closeTagEditor();
}

async function deleteEditingTag() {
  if (!editingTag.value || routeBoardId.value === null) {
    return;
  }

  const confirmed = window.confirm(`Delete tag "${editingTag.value.name}"?\n\nThis removes the tag from all cards and cannot be undone.`);
  if (!confirmed) {
    return;
  }

  const deleted = await deleteTag(editingTag.value.id, routeBoardId.value);
  if (!deleted) {
    return;
  }

  cardStore.removeTagFromCards(editingTag.value.name);
  await closeTagEditor();
}

function clearDraftState() {
  draft.value = null;
  draftEmoji.value = null;
  draftTagName.value = '';
  draftSourceKey.value = null;
}

function initialiseCreateDraftState() {
  if (draftSourceKey.value === 'create' && draft.value !== null) {
    return;
  }

  draft.value = createTagStyleDraft({
    id: 0,
    name: '',
    styleName: 'solid',
    stylePropertiesJson: DEFAULT_TAG_STYLE_PROPERTIES_JSON,
    emoji: null,
    createdAtUtc: '1970-01-01T00:00:00Z',
    updatedAtUtc: '1970-01-01T00:00:00Z'
  });
  draftEmoji.value = null;
  draftTagName.value = '';
  draftSourceKey.value = 'create';
}

function initialiseEditDraftState(tag: Tag, tagId: number) {
  const sourceKey = `edit:${tagId}`;
  if (draftSourceKey.value === sourceKey && draft.value !== null) {
    return;
  }

  draft.value = createTagStyleDraft(tag);
  draftEmoji.value = tag.emoji ?? null;
  draftTagName.value = tag.name;
  draftSourceKey.value = sourceKey;
}

watch(
  [routeBoardId, routeTagId, isCreateMode],
  async ([nextBoardId, nextTagId, nextIsCreate]) => {
    if (nextBoardId === null) {
      clearDraftState();
      await router.replace({ name: 'boards' });
      return;
    }

    if (nextIsCreate) {
      initialiseCreateDraftState();
      return;
    }

    if (nextTagId === null) {
      clearDraftState();
      await router.replace({ name: 'tags', params: { boardId: nextBoardId } });
      return;
    }

    let nextTag = getTagById(nextTagId);
    if (!nextTag) {
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
</style>
