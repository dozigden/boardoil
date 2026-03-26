<template>
  <div class="card-tag-editor-row">
    <div class="card-tag-editor-pills" aria-live="polite">
      <Tag
        v-for="tagName in tagNamesModel"
        :key="tagName"
        :tag-name="tagName"
        class="tag-pill-editable"
      >
        <button
          type="button"
          class="tag-pill-remove"
          :aria-label="`Remove ${tagName}`"
          @click="removeTag(tagName)"
        >
          x
        </button>
      </Tag>
    </div>

    <div class="card-tag-editor-entry">
      <input
        ref="tagEntryInputRef"
        :value="tagEntry"
        :maxlength="maxLength"
        :placeholder="placeholder"
        aria-label="Add tags"
        @focus="emit('focus')"
        @blur="emit('blur')"
        @input="tagEntry = ($event.target as HTMLInputElement).value"
        @keydown.enter.prevent="assignTagEntry"
      />
      <button
        type="button"
        class="card-tag-editor-add"
        aria-label="Add tags"
        title="Add tags"
        :disabled="!hasPendingTagEntry"
        @click="assignTagEntry"
      >
        <Check :size="14" aria-hidden="true" />
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { Check } from 'lucide-vue-next';
import { computed, nextTick, ref } from 'vue';
import Tag from './Tag.vue';
import { mergeTagNames, parseTagInputValues } from '../utils/tagInput';

const props = withDefaults(defineProps<{
  ensureTagsExist: (tagNames: string[]) => Promise<string[]>;
  maxLength?: number;
  placeholder?: string;
}>(), {
  maxLength: 320,
  placeholder: 'add tags'
});

const tagNamesModel = defineModel<string[]>('tagNames', { required: true });
const tagEntry = ref('');

const emit = defineEmits<{
  focus: [];
  blur: [];
}>();

const tagEntryInputRef = ref<HTMLInputElement | null>(null);

const hasPendingTagEntry = computed(() => tagEntry.value.trim().length > 0);

async function assignTagEntry() {
  const parsedTags = parseTagInputValues([tagEntry.value]);
  if (parsedTags.length === 0) {
    return;
  }

  const ensuredTags = await props.ensureTagsExist(parsedTags);
  if (ensuredTags.length === 0) {
    return;
  }

  tagNamesModel.value = mergeTagNames(tagNamesModel.value, ensuredTags);
  tagEntry.value = '';

  await nextTick();
  tagEntryInputRef.value?.focus();
}

function removeTag(tagName: string) {
  tagNamesModel.value = tagNamesModel.value.filter(x => x !== tagName);
}
</script>

<style scoped>
.card-tag-editor-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
  min-width: 0;
}

.card-tag-editor-pills {
  display: flex;
  align-items: center;
  gap: 0.35rem;
  flex-wrap: wrap;
  flex: 0 1 auto;
  min-width: 0;
}

.card-tag-editor-entry {
  display: inline-flex;
  align-items: stretch;
  align-self: center;
  flex: 0 0 auto;
}

.card-tag-editor-entry input {
  width: 12rem;
  min-width: 10rem;
  margin: 0;
  border-top-right-radius: 0;
  border-bottom-right-radius: 0;
}

.card-tag-editor-add {
  width: 2rem;
  min-width: 2rem;
  margin: 0;
  border-top-left-radius: 0;
  border-bottom-left-radius: 0;
  border-left: none;
  display: inline-flex;
  align-items: center;
  justify-content: center;
}

.card-tag-editor-add:disabled {
  border-color: var(--bo-border-soft);
  background: var(--bo-surface-muted);
  color: var(--bo-ink-subtle);
  cursor: not-allowed;
}
</style>
