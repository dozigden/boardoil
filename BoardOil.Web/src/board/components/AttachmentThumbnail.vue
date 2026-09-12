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
import { toRef } from 'vue';
import type { CardAttachment } from '../../shared/types/attachmentTypes';
import { useAttachmentThumbnail } from '../composables/useAttachmentThumbnail';

const props = defineProps<{
  boardId: number;
  cardId: number;
  archived: boolean;
  attachment: CardAttachment;
  canThumbnail: boolean;
}>();
const { imageUrl } = useAttachmentThumbnail({
  boardId: toRef(props, 'boardId'),
  cardId: toRef(props, 'cardId'),
  archived: toRef(props, 'archived'),
  attachment: () => props.attachment,
  enabled: toRef(props, 'canThumbnail')
});
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
