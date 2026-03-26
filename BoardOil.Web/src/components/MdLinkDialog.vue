<template>
  <div v-if="open" class="md-link-dialog-backdrop" @click.self="emit('cancel')">
    <div class="md-link-dialog" role="dialog" aria-modal="true" aria-labelledby="md-link-dialog-title" @keydown.esc.prevent="emit('cancel')">
      <h4 id="md-link-dialog-title" class="md-link-dialog-title">Edit Link</h4>

      <label>
        Text
        <input
          ref="textInputRef"
          :value="draftText"
          maxlength="5000"
          @input="draftText = ($event.target as HTMLInputElement).value"
          @keydown.enter.prevent="onSave"
        />
      </label>

      <label>
        URL
        <input
          :value="draftUrl"
          placeholder="https://example.com"
          @input="onUrlInput(($event.target as HTMLInputElement).value)"
          @keydown.enter.prevent="onSave"
        />
      </label>

      <p v-if="errorMessage" class="md-link-dialog-error" role="alert">{{ errorMessage }}</p>

      <div class="md-link-dialog-actions">
        <button type="button" class="ghost" @click="emit('cancel')">Cancel</button>
        <button v-if="canRemove" type="button" class="ghost" @click="emit('remove')">Remove link</button>
        <button type="button" :disabled="draftUrl.trim().length === 0" @click="onSave">Save</button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { nextTick, ref, watch } from 'vue';
import { normaliseHttpUrl } from '../utils/linkUrl';

const props = defineProps<{
  open: boolean;
  initialText: string;
  initialUrl: string;
  canRemove: boolean;
}>();

const emit = defineEmits<{
  cancel: [];
  save: [value: { text: string; url: string }];
  remove: [];
}>();

const textInputRef = ref<HTMLInputElement | null>(null);
const draftText = ref('');
const draftUrl = ref('');
const errorMessage = ref('');

function onUrlInput(value: string) {
  draftUrl.value = value;
  errorMessage.value = '';
}

function onSave() {
  const normalisedUrl = normaliseHttpUrl(draftUrl.value.trim());
  if (!normalisedUrl) {
    errorMessage.value = 'Only http:// and https:// links are supported.';
    return;
  }

  emit('save', {
    text: draftText.value,
    url: normalisedUrl
  });
}

watch(
  () => props.open,
  async nextOpen => {
    if (!nextOpen) {
      return;
    }

    draftText.value = props.initialText;
    draftUrl.value = props.initialUrl;
    errorMessage.value = '';

    await nextTick();
    textInputRef.value?.focus();
    textInputRef.value?.select();
  }
);
</script>

<style scoped>
.md-link-dialog-backdrop {
  position: fixed;
  inset: 0;
  z-index: 30;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(53, 22, 90, 0.45);
}

.md-link-dialog {
  width: min(30rem, calc(100vw - 2rem));
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  border: 1px solid var(--bo-border-soft);
  border-radius: 12px;
  padding: 0.85rem;
  background: var(--bo-surface-base);
}

.md-link-dialog-title {
  margin: 0;
  color: var(--bo-link);
}

.md-link-dialog-error {
  margin: 0;
  color: var(--bo-colour-danger-ink);
  font-size: 0.86rem;
}

.md-link-dialog-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.35rem;
}

.md-link-dialog-actions button {
  width: auto;
}
</style>
