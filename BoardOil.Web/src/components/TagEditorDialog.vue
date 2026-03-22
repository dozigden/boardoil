<template>
  <ModalDialog :open="editingTag !== null" :title="dialogTitle" close-label="Cancel editing" @close="closeTagEditor" @submit="saveTag">
    <template v-if="editingTag && draft">
      <div class="tags-dialog-preview">
        <span class="card-id">Preview</span>
        <span class="tag-pill" :style="previewStyle">
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
            :value="draft.backgroundColor"
            :disabled="busy"
            maxlength="7"
            placeholder="#RRGGBB"
            @input="setDraftField('backgroundColor', ($event.target as HTMLInputElement).value)"
          />
        </label>
      </template>

      <template v-else>
        <label>
          Left Color
          <input
            :value="draft.leftColor"
            :disabled="busy"
            maxlength="7"
            placeholder="#RRGGBB"
            @input="setDraftField('leftColor', ($event.target as HTMLInputElement).value)"
          />
        </label>
        <label>
          Right Color
          <input
            :value="draft.rightColor"
            :disabled="busy"
            maxlength="7"
            placeholder="#RRGGBB"
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
          :value="draft.textColor"
          :disabled="busy"
          maxlength="7"
          placeholder="#RRGGBB"
          @input="setDraftField('textColor', ($event.target as HTMLInputElement).value)"
        />
      </label>
    </template>

    <template #actions>
      <div v-if="editingTag" class="editor-actions card-modal-actions">
        <div class="card-modal-actions-left">
          <button type="submit" class="card-modal-save" :disabled="busy" aria-label="Save tag style" title="Save tag style">
            <Check :size="16" aria-hidden="true" />
            <span>Save</span>
          </button>
          <button type="button" class="ghost card-modal-cancel" :disabled="busy" aria-label="Cancel editing" title="Cancel" @click="closeTagEditor">
            <X :size="16" aria-hidden="true" />
            <span>Cancel</span>
          </button>
        </div>
      </div>
    </template>
  </ModalDialog>
</template>

<script setup lang="ts">
import { Check, X } from 'lucide-vue-next';
import { storeToRefs } from 'pinia';
import { computed, onMounted, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useTagStore } from '../stores/tagStore';
import type { TagStyleName } from '../types/boardTypes';
import { buildStylePropertiesJsonFromDraft, createTagStyleDraft, getTagPillStyle } from '../utils/tagStyles';
import type { TagStyleDraft } from '../utils/tagStyles';
import ModalDialog from './ModalDialog.vue';

const route = useRoute();
const router = useRouter();
const tagStore = useTagStore();
const { busy } = storeToRefs(tagStore);
const { updateTagStyle, getTagById, loadTags } = tagStore;
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
  await router.push({ name: 'tags' });
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

  const updatedTag = await updateTagStyle(
    editingTag.value.id,
    draft.value.styleName,
    buildStylePropertiesJsonFromDraft(draft.value)
  );
  if (!updatedTag) {
    return;
  }

  await closeTagEditor();
}

onMounted(async () => {
  if (editingTag.value === null) {
    await loadTags();
  }

  attemptedLoad.value = true;
});

watch(
  [routeTagId, editingTag, attemptedLoad],
  ([nextTagId, nextTag, hasAttemptedLoad], [previousTagId]) => {
    if (nextTagId === null) {
      draft.value = null;
      void router.replace({ name: 'tags' });
      return;
    }

    if (!nextTag) {
      draft.value = null;
      if (hasAttemptedLoad) {
        void router.replace({ name: 'tags' });
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
