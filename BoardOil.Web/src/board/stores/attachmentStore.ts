import { defineStore } from 'pinia';
import { ref } from 'vue';
import { createBoardApi } from '../../shared/api/boardApi';
import type { CardAttachment } from '../../shared/types/attachmentTypes';
import { formatAttachmentSize } from '../utils/formatAttachmentSize';
import { createAttachmentThumbnail } from '../utils/attachmentThumbnails';
import { isSupportedImageFileName } from '../../shared/components/markdownImages';
import { useCardAttachmentThumbnailStore } from './cardAttachmentThumbnailStore';

type Upload = { file: File; progress: number; status: 'queued' | 'uploading' };
type AttachmentContext = { boardId: number; cardId: number; archived: boolean };

export const useAttachmentStore = defineStore('attachments', () => {
  const api = createBoardApi();
  const cardAttachmentThumbnailStore = useCardAttachmentThumbnailStore();
  const supported = api.supportsAttachments === true;
  const context = ref<AttachmentContext | null>(null);
  const items = ref<CardAttachment[]>([]);
  const activeUploads = ref<Upload[]>([]);
  const warningMessages = ref<string[]>([]);
  const maxUploadByteLength = ref(0);
  const loading = ref(false);
  const busy = ref(false);
  let requestVersion = 0;
  let eventVersion = 0;
  let runVersion = 0;
  let controller: AbortController | null = null;

  function cancel() {
    runVersion++;
    controller?.abort();
    controller = null;
    busy.value = false;
    activeUploads.value = [];
  }

  function clearWarnings() { warningMessages.value = []; }

  function clear() {
    requestVersion++;
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
    items.value = result.data.items;
    maxUploadByteLength.value = result.data.maxUploadByteLength;
  }

  function added(boardId: number, cardId: number, attachment: CardAttachment) {
    if (context.value?.boardId !== boardId || context.value.cardId !== cardId || context.value.archived) { return; }
    eventVersion++;
    items.value = [...items.value.filter(item => item.id !== attachment.id), attachment]
      .sort((a, b) => a.createdAtUtc.localeCompare(b.createdAtUtc) || a.id - b.id);
  }

  function removed(boardId: number, cardId: number, attachmentId: number) {
    if (context.value?.boardId !== boardId || context.value.cardId !== cardId) { return; }
    eventVersion++;
    items.value = items.value.filter(item => item.id !== attachmentId);
  }

  function cardRemoved(boardId: number, cardId: number) {
    if (context.value?.boardId === boardId && context.value.cardId === cardId && !context.value.archived) { clear(); }
  }

  function warnOversized(file: File) {
    warningMessages.value.push(`${file.name}: The file exceeds the upload limit of ${formatAttachmentSize(maxUploadByteLength.value)} per file and was not uploaded.`);
  }

  async function runQueue(onProgress?: (file: File, percent: number) => void) {
    const current = context.value;
    if (busy.value || !current || current.archived || !supported) { return []; }
    const completed: CardAttachment[] = [];
    let refreshThumbnailProjection = false;
    const version = ++runVersion;
    busy.value = true;
    while (version === runVersion) {
      const item = activeUploads.value.find(upload => upload.status === 'queued');
      if (!item) { break; }
      if (item.file.size > maxUploadByteLength.value) {
        warnOversized(item.file);
        activeUploads.value = activeUploads.value.filter(upload => upload !== item);
        continue;
      }
      controller = new AbortController();
      item.status = 'uploading';
      let thumbnail: Blob | null = null;
      if (isSupportedImageFileName(item.file.name)) {
        try { thumbnail = await createAttachmentThumbnail(item.file); }
        catch { /* The original remains uploadable when the browser cannot decode the image. */ }
      }
      const result = await api.uploadAttachment(current.boardId, current.cardId, item.file,
        percent => {
          item.progress = percent;
          onProgress?.(item.file, percent);
        }, controller.signal, thumbnail);
      if (version !== runVersion) { return completed; }
      activeUploads.value = activeUploads.value.filter(upload => upload !== item);
      if (result.ok) {
        added(current.boardId, current.cardId, result.data);
        cardAttachmentThumbnailStore.attachmentAdded(current.boardId, current.cardId, result.data);
        completed.push(result.data);
      } else if (result.error.statusCode === 413) {
        warnOversized(item.file);
      } else if (result.error.kind === 'network' || result.error.kind === 'parse') {
        refreshThumbnailProjection = true;
        warningMessages.value.push(`${item.file.name}: The upload could not be confirmed. Check the attachment list before uploading again.`);
      } else {
        warningMessages.value.push(`${item.file.name}: ${result.error.message}`);
      }
    }
    controller = null;
    // A lost response may still have committed. Refresh the saved list, without retaining failed entries.
    await reload();
    if (refreshThumbnailProjection && version === runVersion && context.value === current) {
      await cardAttachmentThumbnailStore.refreshCards(current.boardId, [current.cardId]);
    }
    if (version === runVersion) { busy.value = false; }
    return completed;
  }

  async function upload(files: File[], onProgress?: (file: File, percent: number) => void) {
    if (!supported || !context.value || context.value.archived) { return []; }
    const accepted: File[] = [];
    for (const file of files) {
      if (file.size > maxUploadByteLength.value) { warnOversized(file); }
      else { accepted.push(file); }
    }
    if (accepted.length === 0) { return []; }
    activeUploads.value.push(...accepted.map(file => ({ file, progress: 0, status: 'queued' as const })));
    return await runQueue(onProgress);
  }

  async function remove(attachmentId: number) {
    const current = context.value;
    if (!current || current.archived || busy.value) { return; }
    const result = await api.deleteAttachment(current.boardId, current.cardId, attachmentId);
    if (context.value !== current) { return; }
    if (!result.ok) {
      warningMessages.value.push(`Attachment could not be deleted: ${result.error.message}`);
      return;
    }
    removed(current.boardId, current.cardId, attachmentId);
    await cardAttachmentThumbnailStore.attachmentDeleted(current.boardId, current.cardId, attachmentId);
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

  return { supported, items, activeUploads, warningMessages, clearWarnings, maxUploadByteLength, loading, busy,
    open, reload, clear, cancel, upload, remove, download, added, removed, cardRemoved };
});
