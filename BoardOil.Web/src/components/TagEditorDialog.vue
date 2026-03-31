<template>
  <ModalDialog :open="editingTag !== null" :title="dialogTitle" close-label="Cancel editing" @close="closeTagEditor" @submit="saveTag">
    <template v-if="editingTag && draft">
      <div class="tags-dialog-preview">
        <span class="badge">Preview</span>
        <span class="tag" :style="previewStyle">
          {{ editingTag.name }}
        </span>
      </div>

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
      <div v-if="editingTag" class="editor-actions card-modal-actions">
        <button type="button" class="btn btn--danger" :disabled="busy" aria-label="Delete tag" title="Delete tag" @click="deleteEditingTag">
          <Trash2 :size="16" aria-hidden="true" />
        </button>
        <div class="card-modal-actions-left">
          <button type="submit" class="btn" :disabled="busy" aria-label="Save tag style" title="Save tag style">
            <Check :size="16" aria-hidden="true" />
            <span>Save</span>
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
import { computed, onMounted, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useBoardStore } from '../stores/boardStore';
import { useTagStore } from '../stores/tagStore';
import type { TagStyleName } from '../types/boardTypes';
import { buildStylePropertiesJsonFromDraft, createTagStyleDraft, getTagPillStyle } from '../utils/tagStyles';
import type { TagStyleDraft } from '../utils/tagStyles';
import ModalDialog from './ModalDialog.vue';

const route = useRoute();
const router = useRouter();
const boardStore = useBoardStore();
const tagStore = useTagStore();
const { busy } = storeToRefs(tagStore);
const { updateTagStyle, deleteTag, getTagById, loadTags } = tagStore;
const draft = ref<TagStyleDraft | null>(null);
const attemptedLoad = ref(false);

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
const dialogTitle = computed(() => (editingTag.value ? `Edit Tag: ${editingTag.value.name}` : 'Edit Tag'));

const previewStyle = computed(() => {
  if (!editingTag.value || !draft.value) {
    return getTagPillStyle(editingTag.value);
  }

  return getTagPillStyle({
    id: editingTag.value.id,
    name: editingTag.value.name,
    styleName: draft.value.styleName,
    stylePropertiesJson: buildStylePropertiesJsonFromDraft(draft.value),
    createdAtUtc: editingTag.value.createdAtUtc,
    updatedAtUtc: editingTag.value.updatedAtUtc
  });
});

async function closeTagEditor() {
  const boardId = routeBoardId.value;
  if (boardId === null) {
    await router.push({ name: 'boards' });
    return;
  }

  await router.push({ name: 'tags', params: { boardId } });
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
  if (!editingTag.value || !draft.value) {
    return;
  }

  if (routeBoardId.value === null) {
    return;
  }

  const updatedTag = await updateTagStyle(
    editingTag.value.id,
    draft.value.styleName,
    buildStylePropertiesJsonFromDraft(draft.value),
    routeBoardId.value
  );
  if (!updatedTag) {
    return;
  }

  await closeTagEditor();
}

async function deleteEditingTag() {
  if (!editingTag.value) {
    return;
  }

  if (routeBoardId.value === null) {
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

  boardStore.removeTagFromCards(editingTag.value.name);
  await closeTagEditor();
}

onMounted(async () => {
  if (editingTag.value === null && routeBoardId.value !== null) {
    await loadTags(routeBoardId.value);
  }

  attemptedLoad.value = true;
});

watch(
  [routeBoardId, routeTagId, editingTag, attemptedLoad],
  ([nextBoardId, nextTagId, nextTag, hasAttemptedLoad], [, previousTagId]) => {
    if (nextBoardId === null) {
      draft.value = null;
      void router.replace({ name: 'boards' });
      return;
    }

    if (nextTagId === null) {
      draft.value = null;
      void router.replace({ name: 'tags', params: { boardId: nextBoardId } });
      return;
    }

    if (!nextTag) {
      draft.value = null;
      if (hasAttemptedLoad) {
        void router.replace({ name: 'tags', params: { boardId: nextBoardId } });
      }
      return;
    }

    if (previousTagId !== nextTagId || draft.value === null) {
      draft.value = createTagStyleDraft(nextTag);
    }
  },
  { immediate: true }
);
</script>
