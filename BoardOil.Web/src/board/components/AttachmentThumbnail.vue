<template>
  <span class="attachment-thumbnail">
    <img v-if="imageUrl" :src="imageUrl" :alt="`Preview of ${attachment.originalFileName}`" />
    <span v-else class="attachment-thumbnail-placeholder" aria-hidden="true">
      <FileIcon :size="28" />
    </span>
  </span>
</template>

<script setup lang="ts">
import { File as FileIcon } from '@lucide/vue';
import { onBeforeUnmount, ref, watch } from 'vue';
import { createBoardApi } from '../../shared/api/boardApi';
import type { CardAttachment } from '../../shared/types/attachmentTypes';
import { attachmentThumbnailQueue, createAttachmentThumbnail } from '../utils/attachmentThumbnails';

const props = defineProps<{
  boardId: number;
  cardId: number;
  archived: boolean;
  attachment: CardAttachment;
  canThumbnail: boolean;
}>();
const api = createBoardApi();
const imageUrl = ref<string | null>(null);
let loadVersion = 0;

watch(() => [props.boardId, props.cardId, props.archived, props.attachment.id, props.attachment.hasThumbnail,
  props.canThumbnail] as const,
  () => { void load(); }, { immediate: true });
onBeforeUnmount(() => {
  loadVersion++;
  clearImage();
});

async function load() {
  const version = ++loadVersion;
  clearImage();
  if (!props.canThumbnail) { return; }
  if (props.attachment.hasThumbnail) {
    const existing = await api.getAttachmentThumbnail(props.boardId, props.attachment.id);
    if (version !== loadVersion) { return; }
    if (existing.ok) { show(existing.data); return; }
    if (existing.error.statusCode !== 404) { return; }
  }

  const key = `${props.boardId}:${props.attachment.id}`;
  const stored = await attachmentThumbnailQueue.run(key, async () => {
    const original = await api.getAttachmentImage(
      props.boardId, props.cardId, props.archived, props.attachment.originalFileName);
    if (!original.ok) { return false; }
    try {
      const thumbnail = await createAttachmentThumbnail(original.data);
      const result = await api.putAttachmentThumbnail(props.boardId, props.attachment.id, thumbnail);
      return result.ok;
    } catch { return false; }
  });
  if (!stored || version !== loadVersion) { return; }
  const result = await api.getAttachmentThumbnail(props.boardId, props.attachment.id);
  if (version === loadVersion && result.ok) { show(result.data); }
}

function show(blob: Blob) {
  clearImage();
  imageUrl.value = URL.createObjectURL(blob);
}

function clearImage() {
  if (imageUrl.value) { URL.revokeObjectURL(imageUrl.value); }
  imageUrl.value = null;
}
</script>

<style scoped>
.attachment-thumbnail {
  display: grid;
  flex: 0 0 48px;
  width: 48px;
  height: 48px;
  place-items: center;
}
.attachment-thumbnail img {
  display: block;
  max-width: 48px;
  max-height: 48px;
  object-fit: contain;
}
.attachment-thumbnail-placeholder {
  display: grid;
  width: 40px;
  height: 40px;
  color: var(--bo-ink-muted);
  background: var(--bo-surface-muted);
  border: 1px solid var(--bo-border-soft);
  border-radius: 0.35rem;
  place-items: center;
}
</style>
