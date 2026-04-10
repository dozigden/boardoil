<template>
  <article class="create-card-inline" :style="draftStyle">
    <div class="create-card-inline-input-row">
      <span v-if="draftEmoji" class="create-card-inline-emoji" aria-hidden="true">{{ draftEmoji }}</span>
      <textarea
        :ref="setInputRef"
        class="create-card-inline-input"
        :value="title"
        rows="1"
        maxlength="200"
        placeholder="Title"
        @input="handleTitleInput"
        @keydown.enter.prevent="emit('save')"
        @keydown.esc.prevent="emit('cancel')"
      />
    </div>

    <div class="editor-actions create-card-inline-actions">
      <button type="button" class="btn create-card-save" aria-label="Save new card" title="Save new card" @click="emit('save')">
        <Check :size="16" aria-hidden="true" />
      </button>
      <button type="button" class="btn btn--secondary create-card-cancel" aria-label="Cancel new card" title="Cancel new card" @click="emit('cancel')">
        <X :size="16" aria-hidden="true" />
      </button>
    </div>
  </article>
</template>

<script setup lang="ts">
import { Check, X } from 'lucide-vue-next';
import { computed, nextTick, ref, watch } from 'vue';
import { useCardTypeStore } from '../stores/cardTypeStore';
import { getCardSurfaceStyle, normaliseCardTypeEmojiForRender } from '../utils/cardTypeStyles';

const props = defineProps<{
  title: string;
  cardTypeId: number | null;
  inputRef?: (element: unknown) => void;
}>();

const emit = defineEmits<{
  'update:title': [value: string];
  save: [];
  cancel: [];
}>();

const cardTypeStore = useCardTypeStore();
const titleInputRef = ref<HTMLTextAreaElement | null>(null);
const resolvedCardType = computed(() => cardTypeStore.getCardTypeById(props.cardTypeId));
const draftStyle = computed(() => getCardSurfaceStyle(resolvedCardType.value));
const draftEmoji = computed(() => normaliseCardTypeEmojiForRender(resolvedCardType.value?.emoji));

function setInputRef(element: unknown) {
  titleInputRef.value = element instanceof HTMLTextAreaElement ? element : null;
  props.inputRef?.(element);
}

function normaliseTitle(value: string) {
  return value.replace(/\r?\n+/g, ' ');
}

function handleTitleInput(event: Event) {
  const target = event.target;
  if (!(target instanceof HTMLTextAreaElement)) {
    return;
  }

  const normalised = normaliseTitle(target.value);
  if (target.value !== normalised) {
    target.value = normalised;
  }

  resizeTitleInput(target);
  emit('update:title', normalised);
}

function resizeTitleInput(element: HTMLTextAreaElement) {
  element.style.height = '0';
  element.style.height = `${element.scrollHeight}px`;
}

watch(
  () => props.title,
  async () => {
    await nextTick();
    if (titleInputRef.value) {
      resizeTitleInput(titleInputRef.value);
    }
  },
  { immediate: true }
);
</script>

<style scoped>
.create-card-inline {
  border: 1px solid var(--bo-border-soft);
  border-radius: 12px;
  padding: 0.6rem;
  background: var(--bo-surface-base);
  margin-bottom: 0.5rem;
  display: grid;
  gap: 0.45rem;
}

.create-card-inline-input-row {
  display: flex;
  align-items: flex-start;
  gap: 0.45rem;
}

.create-card-inline-emoji {
  flex: 0 0 auto;
  font-size: 1rem;
  line-height: 1;
}

.create-card-inline-input {
  width: 100%;
  min-width: 0;
  border: 1px solid var(--bo-border-soft);
  border-radius: 8px;
  background: var(--bo-surface-base);
  color: var(--bo-ink-default);
  font: inherit;
  line-height: 1.35;
  padding: 0.35rem 0.5rem;
  resize: none;
  overflow: hidden;
  white-space: pre-wrap;
  overflow-wrap: anywhere;
}

.create-card-inline-input::placeholder {
  color: var(--bo-ink-muted);
}

.create-card-inline-input:focus {
  outline: 2px solid var(--bo-focus-ring);
  outline-offset: 1px;
}

.create-card-inline-actions {
  justify-content: flex-end;
}

.create-card-save,
.create-card-cancel {
  padding: 0.4rem;
}
</style>
