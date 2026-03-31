<template>
  <div class="card-tag-editor-row">
    <div class="card-tag-editor-pills tag-group" aria-live="polite">
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
        aria-autocomplete="list"
        :aria-expanded="isSuggestionsOpen"
        aria-controls="card-tag-editor-suggestions"
        @focus="handleFocus"
        @blur="handleBlur"
        @input="handleInput"
        @keydown="handleKeydown"
      />
      <button
        type="button"
        class="btn card-tag-editor-add"
        aria-label="Add tags"
        title="Add tags"
        :disabled="!hasPendingTagEntry"
        @click="assignTagEntry"
      >
        <Check :size="14" aria-hidden="true" />
      </button>

      <div
        v-if="isSuggestionsOpen"
        id="card-tag-editor-suggestions"
        class="card-tag-editor-suggestions"
        role="listbox"
        aria-label="Tag suggestions"
      >
        <button
          v-for="(suggestion, index) in tagCompletionSuggestions"
          :key="suggestion"
          type="button"
          class="card-tag-editor-suggestion"
          :class="{ 'is-active': index === activeSuggestionIndex }"
          role="option"
          :aria-selected="index === activeSuggestionIndex"
          @mouseenter="activeSuggestionIndex = index"
          @mousedown.prevent
          @click="selectSuggestion(suggestion)"
        >
          {{ suggestion }}
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { Check } from 'lucide-vue-next';
import { computed, nextTick, ref, watch } from 'vue';
import { storeToRefs } from 'pinia';
import Tag from './Tag.vue';
import { mergeTagNames, parseTagInputValues, getTagCompletionQuery, getTagCompletionSuggestions } from '../utils/tagInput';
import { useTagStore } from '../stores/tagStore';

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
const tagStore = useTagStore();
const { tags } = storeToRefs(tagStore);
const tagEntryFocused = ref(false);
const activeSuggestionIndex = ref(-1);
const availableTagNames = computed(() => tags.value.map(tag => tag.name));
const tagCompletionQuery = computed(() => getTagCompletionQuery(tagEntry.value));
const tagCompletionSuggestions = computed(() => getTagCompletionSuggestions(
  availableTagNames.value,
  tagEntry.value,
  tagNamesModel.value
));
const isSuggestionsOpen = computed(() => tagEntryFocused.value && tagCompletionQuery.value.length > 0 && tagCompletionSuggestions.value.length > 0);

const hasPendingTagEntry = computed(() => tagEntry.value.trim().length > 0);

watch(tagCompletionSuggestions, () => {
  activeSuggestionIndex.value = -1;
});

function handleFocus() {
  tagEntryFocused.value = true;
  emit('focus');
}

function handleBlur() {
  tagEntryFocused.value = false;
  activeSuggestionIndex.value = -1;
  emit('blur');
}

function handleInput(event: Event) {
  tagEntry.value = (event.target as HTMLInputElement).value;
  activeSuggestionIndex.value = -1;
}

async function handleKeydown(event: KeyboardEvent) {
  if (event.key === 'ArrowDown') {
    if (!isSuggestionsOpen.value) {
      return;
    }

    event.preventDefault();
    moveActiveSuggestion(1);
    return;
  }

  if (event.key === 'ArrowUp') {
    if (!isSuggestionsOpen.value) {
      return;
    }

    event.preventDefault();
    moveActiveSuggestion(-1);
    return;
  }

  if (event.key === 'Escape') {
    if (!isSuggestionsOpen.value) {
      return;
    }

    event.preventDefault();
    activeSuggestionIndex.value = -1;
    return;
  }

  if (event.key === 'Enter') {
    event.preventDefault();
    if (isSuggestionsOpen.value && activeSuggestionIndex.value >= 0) {
      await selectSuggestion(tagCompletionSuggestions.value[activeSuggestionIndex.value]);
      return;
    }

    await assignTagEntry();
  }
}

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
  await resetTagEntry();
}

async function selectSuggestion(tagName: string) {
  const parsedTags = parseTagInputValues([getCompletedTagEntryPrefix()]);
  const ensuredTags = await props.ensureTagsExist([...parsedTags, tagName]);
  if (ensuredTags.length === 0) {
    return;
  }

  tagNamesModel.value = mergeTagNames(tagNamesModel.value, ensuredTags);
  await resetTagEntry();
}

function removeTag(tagName: string) {
  tagNamesModel.value = tagNamesModel.value.filter(x => x !== tagName);
}

function moveActiveSuggestion(delta: number) {
  const suggestions = tagCompletionSuggestions.value;
  if (suggestions.length === 0) {
    activeSuggestionIndex.value = -1;
    return;
  }

  if (activeSuggestionIndex.value < 0) {
    activeSuggestionIndex.value = delta > 0 ? 0 : suggestions.length - 1;
    return;
  }

  activeSuggestionIndex.value = (activeSuggestionIndex.value + delta + suggestions.length) % suggestions.length;
}

function getCompletedTagEntryPrefix() {
  const lastCommaIndex = tagEntry.value.lastIndexOf(',');
  if (lastCommaIndex < 0) {
    return '';
  }

  return tagEntry.value.slice(0, lastCommaIndex);
}

async function resetTagEntry() {
  tagEntry.value = '';
  activeSuggestionIndex.value = -1;

  await nextTick();
  tagEntryInputRef.value?.focus();
}
</script>

<style scoped>
.card-tag-editor-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
  min-width: 0;
  overflow: visible;
}

.card-tag-editor-pills {
  flex: 0 1 auto;
  min-width: 0;
}

.card-tag-editor-entry {
  display: inline-flex;
  align-items: stretch;
  align-self: center;
  flex: 0 0 auto;
  position: relative;
  overflow: visible;
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

.card-tag-editor-suggestions {
  position: absolute;
  top: calc(100% + 0.35rem);
  left: 0;
  min-width: 100%;
  max-width: 100%;
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
  padding: 0.35rem;
  border: 1px solid var(--bo-border-default);
  border-radius: 0.8rem;
  background: var(--bo-surface-panel-strong);
  box-shadow: var(--bo-shadow-pop);
  z-index: 3;
}

.card-tag-editor-suggestion {
  margin: 0;
  width: 100%;
  min-width: 0;
  border: none;
  border-radius: 0.55rem;
  background: transparent;
  color: var(--bo-ink-default);
  text-align: left;
  justify-content: flex-start;
  padding: 0.45rem 0.65rem;
  font: inherit;
  cursor: pointer;
}

.card-tag-editor-suggestion:hover,
.card-tag-editor-suggestion:focus-visible,
.card-tag-editor-suggestion.is-active {
  background: var(--bo-colour-energy);
  color: var(--bo-ink-strong);
}
</style>
