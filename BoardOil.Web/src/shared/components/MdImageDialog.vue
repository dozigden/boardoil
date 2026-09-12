<template>
  <FixedChromeDialog
    :open="open"
    title="Image preview"
    size="fill"
    body-mode="managed"
    close-label="Close image"
    @close="emit('close')"
    @submit="save"
  >
    <div class="md-image-dialog-content">
      <div class="md-image-dialog-preview-frame">
        <img
          v-if="sourceUrl && !imageFailed"
          class="md-image-dialog-preview"
          :src="sourceUrl"
          :alt="alt"
          loading="lazy"
          referrerpolicy="no-referrer"
          @error="imageFailed = true"
        />
        <span v-else class="md-image-dialog-unavailable" role="img" :aria-label="alt || 'Image'">
          Image unavailable
        </span>
      </div>

      <label v-if="editable" class="md-image-dialog-alt-field">
        <span>Alternative text</span>
        <input ref="altInputRef" v-model="altDraft" type="text" maxlength="250" />
        <small>Describe the image for people who cannot see it. Leave blank only when it is decorative.</small>
      </label>
    </div>

    <template #actions>
      <div class="fixed-chrome-dialog-actions fixed-chrome-dialog-actions--end">
        <button v-if="editable" type="submit" class="btn">Save alternative text</button>
        <button type="button" class="btn btn--secondary" @click="emit('close')">Close</button>
      </div>
    </template>
  </FixedChromeDialog>
</template>

<script setup lang="ts">
import { nextTick, ref, watch } from 'vue';
import FixedChromeDialog from './FixedChromeDialog.vue';

const props = defineProps<{
  open: boolean;
  sourceUrl: string | null;
  alt: string;
  editable: boolean;
}>();

const emit = defineEmits<{
  close: [];
  save: [alt: string];
}>();

const altDraft = ref('');
const altInputRef = ref<HTMLInputElement | null>(null);
const imageFailed = ref(false);

watch(
  () => [props.open, props.alt, props.sourceUrl] as const,
  ([open, alt]) => {
    altDraft.value = alt;
    imageFailed.value = false;
    if (open && props.editable) {
      void nextTick(() => altInputRef.value?.focus());
    }
  },
  { immediate: true }
);

function save() {
  if (!props.editable) {
    emit('close');
    return;
  }
  emit('save', altDraft.value.replace(/[\\[\]\r\n]/g, ' ').replace(/\s+/g, ' ').trim());
}
</script>

<style scoped>
.md-image-dialog-content {
  display: grid;
  grid-template-rows: minmax(0, 1fr) auto;
  gap: 0.85rem;
  height: 100%;
  min-width: 0;
  min-height: 0;
}

.md-image-dialog-preview-frame {
  display: grid;
  place-items: center;
  min-width: 0;
  min-height: 0;
  overflow: hidden;
}

.md-image-dialog-preview {
  display: block;
  width: 100%;
  height: 100%;
  max-width: 100%;
  max-height: 100%;
  border: 1px solid var(--bo-border-soft);
  border-radius: 10px;
  object-fit: contain;
}

.md-image-dialog-unavailable {
  display: block;
  border: 1px dashed var(--bo-border-default);
  border-radius: 10px;
  padding: 2rem 1rem;
  color: var(--bo-ink-muted);
  text-align: center;
}

.md-image-dialog-alt-field {
  display: grid;
  gap: 0.35rem;
}

.md-image-dialog-alt-field small {
  color: var(--bo-ink-muted);
}
</style>
