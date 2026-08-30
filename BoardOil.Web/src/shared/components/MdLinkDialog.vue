<template>
  <FixedChromeDialog :open="open" title="Edit Link" close-label="Cancel editing link" @close="emit('cancel')" @submit="onSave">
    <label>
      Text
      <input
        ref="textInputRef"
        :value="draftText"
        maxlength="5000"
        autofocus
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

    <template #actions>
      <div class="fixed-chrome-dialog-actions fixed-chrome-dialog-actions--end">
        <button type="button" class="btn btn--secondary" @click="emit('cancel')">Cancel</button>
        <button v-if="canRemove" type="button" class="btn btn--secondary" @click="emit('remove')">Remove link</button>
        <button type="submit" class="btn" :disabled="draftUrl.trim().length === 0">Save</button>
      </div>
    </template>
  </FixedChromeDialog>
</template>

<script setup lang="ts">
import { nextTick, ref, watch } from 'vue';
import FixedChromeDialog from './FixedChromeDialog.vue';
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
.md-link-dialog-error {
  margin: 0;
  color: var(--bo-colour-danger-ink);
  font-size: 0.86rem;
}
</style>
