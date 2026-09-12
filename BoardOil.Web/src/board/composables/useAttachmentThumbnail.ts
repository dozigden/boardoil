import { onBeforeUnmount, ref, toValue, watch, type MaybeRefOrGetter } from 'vue';
import { createBoardApi } from '../../shared/api/boardApi';
import type { CardAttachment } from '../../shared/types/attachmentTypes';
import { attachmentThumbnailQueue, createAttachmentThumbnail } from '../utils/attachmentThumbnails';

type ThumbnailAttachment = Pick<CardAttachment, 'id' | 'originalFileName' | 'hasThumbnail'>;

export function useAttachmentThumbnail(options: {
  boardId: MaybeRefOrGetter<number>;
  cardId: MaybeRefOrGetter<number>;
  archived: MaybeRefOrGetter<boolean>;
  attachment: MaybeRefOrGetter<ThumbnailAttachment | null>;
  enabled: MaybeRefOrGetter<boolean>;
  onThumbnailStored?: (attachmentId: number) => void;
}) {
  const api = createBoardApi();
  const imageUrl = ref<string | null>(null);
  let loadVersion = 0;

  watch(
    () => {
      const attachment = toValue(options.attachment);
      return [
        toValue(options.boardId),
        toValue(options.cardId),
        toValue(options.archived),
        attachment?.id ?? null,
        attachment?.hasThumbnail ?? false,
        toValue(options.enabled)
      ] as const;
    },
    () => { void load(); },
    { immediate: true }
  );

  onBeforeUnmount(() => {
    loadVersion++;
    clearImage();
  });

  async function load() {
    const version = ++loadVersion;
    const boardId = toValue(options.boardId);
    const cardId = toValue(options.cardId);
    const archived = toValue(options.archived);
    const attachment = toValue(options.attachment);
    clearImage();
    if (!toValue(options.enabled) || !attachment || !api.supportsAttachments || boardId <= 0) {
      return;
    }

    if (attachment.hasThumbnail) {
      const existing = await api.getAttachmentThumbnail(boardId, attachment.id);
      if (version !== loadVersion) { return; }
      if (existing.ok) {
        show(existing.data);
        return;
      }
      if (existing.error.statusCode !== 404) { return; }
    }

    const key = `${boardId}:${attachment.id}`;
    const stored = await attachmentThumbnailQueue.run(key, async () => {
      const original = await api.getAttachmentImage(boardId, cardId, archived, attachment.originalFileName);
      if (!original.ok) { return false; }
      try {
        const thumbnail = await createAttachmentThumbnail(original.data);
        const result = await api.putAttachmentThumbnail(boardId, attachment.id, thumbnail);
        return result.ok;
      } catch {
        return false;
      }
    });
    if (!stored) { return; }
    options.onThumbnailStored?.(attachment.id);
    if (version !== loadVersion) { return; }

    const result = await api.getAttachmentThumbnail(boardId, attachment.id);
    if (version === loadVersion && result.ok) {
      show(result.data);
    }
  }

  function show(blob: Blob) {
    clearImage();
    imageUrl.value = URL.createObjectURL(blob);
  }

  function clearImage() {
    if (imageUrl.value) {
      URL.revokeObjectURL(imageUrl.value);
    }
    imageUrl.value = null;
  }

  return { imageUrl };
}
