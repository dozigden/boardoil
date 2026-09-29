import { defineStore } from 'pinia';
import { ref } from 'vue';
import { createBoardApi } from '../../shared/api/boardApi';
import type { CardAttachment } from '../../shared/types/attachmentTypes';
import { formatAttachmentSize } from '../utils/formatAttachmentSize';
import { createAttachmentThumbnail } from '../utils/attachmentThumbnails';
import { isSupportedImageFileName } from '../../shared/components/markdownImages';
import { useCardAttachmentThumbnailStore } from './cardAttachmentThumbnailStore';

type Upload = { file: File; progress: number; status: 'queued' | 'uploading' };
type UploadBatch = {
  uploads: Upload[];
  completed: CardAttachment[];
  onProgress?: (file: File, percent: number) => void;
  resolve: (attachments: CardAttachment[]) => void;
  reject: (reason: unknown) => void;
};
type AttachmentContext = { boardId: number; cardId: number; archived: boolean };

export const useAttachmentStore = defineStore('attachments', () => {
  const api = createBoardApi();
  const cardAttachmentThumbnailStore = useCardAttachmentThumbnailStore();
  const supported = api.supportsAttachments === true;
  const mutable = supported && api.supportsAttachmentMutations !== false;
  const context = ref<AttachmentContext | null>(null);
  const items = ref<CardAttachment[]>([]);
  const activeUploads = ref<Upload[]>([]);
  const warningMessages = ref<string[]>([]);
  const maxUploadByteLength = ref(0);
  const loading = ref(false);
  const busy = ref(false);
  const revision = ref(0);
  let requestVersion = 0;
  let eventVersion = 0;
  let runVersion = 0;
  let controller: AbortController | null = null;
  let uploadBatches: UploadBatch[] = [];

  function cancel() {
    runVersion++;
    controller?.abort();
    controller = null;
    busy.value = false;
    activeUploads.value = [];
    for (const batch of uploadBatches) { batch.resolve(batch.completed); }
    uploadBatches = [];
  }

  function clearWarnings() { warningMessages.value = []; }

  function clear() {
    requestVersion++;
    revision.value++;
    cancel();
    context.value = null;
    items.value = [];
    clearWarnings();
    maxUploadByteLength.value = 0;
    loading.value = false;
  }

  async function open(boardId: number, cardId: number, archived = false) {
    clear();
    context.value = { boardId, cardId, archived };
    await reload();
  }

  async function reload() {
    const current = context.value;
    if (!supported || !current) { return; }
    const version = ++requestVersion;
    const eventsAtStart = eventVersion;
    loading.value = true;
    const result = await api.getAttachments(current.boardId, current.cardId, current.archived);
    if (version !== requestVersion) { return; }
    if (eventsAtStart !== eventVersion) { await reload(); return; }
    loading.value = false;
    if (!result.ok) {
      warningMessages.value.push(`Attachments could not be loaded: ${result.error.message}`);
      return;
    }
    const nextItems = result.data.items;
    const attachmentSetChanged = nextItems.length !== items.value.length
      || nextItems.some((item, index) => item.id !== items.value[index]?.id
        || item.originalFileName !== items.value[index]?.originalFileName);
    items.value = nextItems;
    if (attachmentSetChanged) {
      revision.value++;
    }
    maxUploadByteLength.value = result.data.maxUploadByteLength;
  }

  function added(boardId: number, cardId: number, attachment: CardAttachment) {
    if (context.value?.boardId === boardId && context.value.cardId === cardId && !context.value.archived) {
      eventVersion++;
      items.value = [...items.value.filter(item => item.id !== attachment.id), attachment]
        .sort((a, b) => a.createdAtUtc.localeCompare(b.createdAtUtc) || a.id - b.id);
      revision.value++;
    }
    cardAttachmentThumbnailStore.attachmentAdded(boardId, cardId, attachment);
  }

  async function removed(boardId: number, cardId: number, attachmentId: number) {
    if (context.value?.boardId === boardId && context.value.cardId === cardId) {
      eventVersion++;
      items.value = items.value.filter(item => item.id !== attachmentId);
      revision.value++;
    }
    await cardAttachmentThumbnailStore.attachmentDeleted(boardId, cardId, attachmentId);
  }

  function cardRemoved(boardId: number, cardId: number) {
    if (context.value?.boardId === boardId && context.value.cardId === cardId && !context.value.archived) { clear(); }
  }

  function warnOversized(file: File) {
    warningMessages.value.push(`${file.name}: The file exceeds the upload limit of ${formatAttachmentSize(maxUploadByteLength.value)} per file and was not uploaded.`);
  }

  async function runQueue(current: AttachmentContext) {
    const version = ++runVersion;
    busy.value = true;

    function isCurrentRun() {
      return version === runVersion && context.value === current;
    }

    try {
      while (isCurrentRun() && uploadBatches.length > 0) {
        const batch = uploadBatches[0]!;
        let refreshThumbnailProjection = false;
        for (const item of batch.uploads) {
          if (item.file.size > maxUploadByteLength.value) {
            warnOversized(item.file);
            activeUploads.value = activeUploads.value.filter(upload => upload !== item);
            continue;
          }
          const uploadController = new AbortController();
          controller = uploadController;
          item.status = 'uploading';
          let thumbnail: Blob | null = null;
          if (isSupportedImageFileName(item.file.name)) {
            try { thumbnail = await createAttachmentThumbnail(item.file); }
            catch { /* The original remains uploadable when the browser cannot decode the image. */ }
          }
          if (!isCurrentRun()) { return; }
          const result = await api.uploadAttachment(current.boardId, current.cardId, item.file,
            percent => {
              if (!isCurrentRun()) { return; }
              item.progress = percent;
              batch.onProgress?.(item.file, percent);
            }, uploadController.signal, thumbnail);
          if (!isCurrentRun()) { return; }
          controller = null;
          activeUploads.value = activeUploads.value.filter(upload => upload !== item);
          if (result.ok) {
            added(current.boardId, current.cardId, result.data);
            batch.completed.push(result.data);
          } else if (result.error.statusCode === 413) {
            warnOversized(item.file);
          } else if (result.error.kind === 'network' || result.error.kind === 'parse') {
            refreshThumbnailProjection = true;
            warningMessages.value.push(`${item.file.name}: The upload could not be confirmed. Check the attachment list before uploading again.`);
          } else {
            warningMessages.value.push(`${item.file.name}: ${result.error.message}`);
          }
        }
        // Reconcile this batch before completing its caller, including uploads with lost responses.
        await reload();
        if (!isCurrentRun()) { return; }
        if (refreshThumbnailProjection) {
          await cardAttachmentThumbnailStore.refreshCards(current.boardId, [current.cardId]);
          if (!isCurrentRun()) { return; }
        }
        uploadBatches.shift();
        batch.resolve(batch.completed);
        // Batches added during reconciliation remain in the same draining lifecycle.
      }
    } catch (error) {
      if (isCurrentRun()) {
        for (const batch of uploadBatches) { batch.reject(error); }
        uploadBatches = [];
        activeUploads.value = [];
      }
    } finally {
      if (isCurrentRun()) {
        controller = null;
        busy.value = false;
      }
    }
  }

  async function upload(files: File[], onProgress?: (file: File, percent: number) => void) {
    const current = context.value;
    if (!supported || !mutable || !current || current.archived) { return []; }
    const accepted: File[] = [];
    for (const file of files) {
      if (file.size > maxUploadByteLength.value) { warnOversized(file); }
      else { accepted.push(file); }
    }
    if (accepted.length === 0) { return []; }
    activeUploads.value.push(...accepted.map(file => ({ file, progress: 0, status: 'queued' as const })));
    const uploads = activeUploads.value.slice(-accepted.length);
    return await new Promise<CardAttachment[]>((resolve, reject) => {
      uploadBatches.push({ uploads, completed: [], onProgress, resolve, reject });
      if (!busy.value) { void runQueue(current); }
    });
  }

  async function remove(attachmentId: number) {
    const current = context.value;
    if (!current || current.archived || busy.value || !mutable) { return false; }
    const result = await api.deleteAttachment(current.boardId, current.cardId, attachmentId);
    if (context.value !== current) { return false; }
    if (!result.ok) {
      warningMessages.value.push(`Attachment could not be deleted: ${result.error.message}`);
      return false;
    }
    await removed(current.boardId, current.cardId, attachmentId);
    return true;
  }

  async function download(attachmentId: number) {
    const current = context.value;
    if (!current) { return; }
    const result = await api.downloadAttachment(current.boardId, attachmentId);
    if (context.value !== current) { return; }
    if (!result.ok) {
      warningMessages.value.push(`Attachment could not be downloaded: ${result.error.message}`);
      return;
    }
    const url = URL.createObjectURL(result.data.blob);
    const link = document.createElement('a');
    link.href = url; link.download = result.data.fileName;
    link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }

  return { supported, mutable, items, activeUploads, warningMessages, clearWarnings, maxUploadByteLength, loading, busy, revision,
    open, reload, clear, cancel, upload, remove, download, added, removed, cardRemoved };
});
