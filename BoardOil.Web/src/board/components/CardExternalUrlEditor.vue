<template>
  <div class="card-external-url-editor">
    <span class="card-editor-field-label">External link</span>

    <div v-if="!isEditing" class="card-external-url-display">
      <a
        v-if="externalUrlModel"
        class="card-external-url-link"
        :href="externalUrlModel"
        target="_blank"
        rel="noopener noreferrer"
      >
        {{ externalUrlModel }}
      </a>
      <span v-else class="card-external-url-empty">-</span>
      <button type="button" class="btn btn--secondary card-external-url-edit" @click="beginEdit">
        <Pencil :size="14" aria-hidden="true" />
        <span>{{ externalUrlModel ? 'Edit' : 'Add' }}</span>
      </button>
    </div>

    <div v-else class="card-external-url-edit-row">
      <input
        ref="inputRef"
        :value="draftUrl"
        type="url"
        inputmode="url"
        aria-label="External URL"
        placeholder="https://example.com"
        @input="handleInput"
        @keydown.enter.prevent="finishEdit"
        @keydown.esc.stop.prevent="cancelEdit"
      />
      <button type="button" class="btn card-external-url-action" aria-label="Apply external URL" title="Apply" @click="finishEdit">
        <Check :size="14" aria-hidden="true" />
      </button>
      <button type="button" class="btn btn--secondary card-external-url-action" aria-label="Cancel external URL edit" title="Cancel" @click="cancelEdit">
        <X :size="14" aria-hidden="true" />
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { Check, Pencil, X } from 'lucide-vue-next';
import { nextTick, ref, watch } from 'vue';
import { isHttpOrHttpsUrl } from '../../shared/utils/linkUrl';

const externalUrlModel = defineModel<string | null>('externalUrl', { required: true });

const isEditing = ref(false);
const draftUrl = ref('');
const inputRef = ref<HTMLInputElement | null>(null);

async function beginEdit() {
  draftUrl.value = externalUrlModel.value ?? '';
  isEditing.value = true;
  await nextTick();
  syncInputValidity();
  inputRef.value?.focus();
  inputRef.value?.select();
}

function handleInput(event: Event) {
  const target = event.target;
  if (!(target instanceof HTMLInputElement)) {
    return;
  }

  draftUrl.value = target.value;
  syncInputValidity();
}

function finishEdit() {
  const normalisedUrl = normaliseDraftValue(draftUrl.value);
  if (normalisedUrl !== null && !isHttpOrHttpsUrl(normalisedUrl)) {
    syncInputValidity();
    inputRef.value?.reportValidity();
    return;
  }

  externalUrlModel.value = normalisedUrl;
  draftUrl.value = normalisedUrl ?? '';
  isEditing.value = false;
}

function cancelEdit() {
  draftUrl.value = externalUrlModel.value ?? '';
  isEditing.value = false;
}

function syncInputValidity() {
  const input = inputRef.value;
  if (!input) {
    return;
  }

  const normalisedUrl = normaliseDraftValue(draftUrl.value);
  const validationMessage = normalisedUrl === null || isHttpOrHttpsUrl(normalisedUrl)
    ? ''
    : 'External URL must be an absolute HTTP or HTTPS URL.';
  input.setCustomValidity(validationMessage);
}

function normaliseDraftValue(value: string) {
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}

watch(
  externalUrlModel,
  nextExternalUrl => {
    if (!isEditing.value) {
      draftUrl.value = nextExternalUrl ?? '';
    }
  }
);
</script>

<style scoped>
.card-external-url-editor {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  min-width: 0;
}

.card-editor-field-label {
  font-size: 0.85rem;
  color: var(--bo-ink-muted);
}

.card-external-url-display,
.card-external-url-edit-row {
  display: flex;
  align-items: center;
  gap: 0.35rem;
  min-width: 0;
}

.card-external-url-link {
  min-width: 0;
  flex: 1 1 auto;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.card-external-url-empty {
  flex: 1 1 auto;
  color: var(--bo-ink-subtle);
}

.card-external-url-edit {
  flex: 0 0 auto;
  padding: 0.3rem 0.45rem;
}

.card-external-url-edit-row input {
  min-width: 0;
  flex: 1 1 auto;
  border: 1px solid var(--bo-border-soft);
  border-radius: 6px;
  background: var(--bo-surface-base);
  color: var(--bo-ink-default);
  font: inherit;
  padding: 0.35rem 0.45rem;
}

.card-external-url-edit-row input:focus {
  outline: 2px solid var(--bo-focus-ring);
  outline-offset: 1px;
}

.card-external-url-action {
  flex: 0 0 auto;
  padding: 0.35rem;
}
</style>
