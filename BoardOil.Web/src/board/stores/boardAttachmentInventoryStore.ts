import { defineStore } from 'pinia';
import { ref } from 'vue';
import { createBoardApi } from '../../shared/api/boardApi';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import { defaultBoardAttachmentInventoryQuery, type BoardAttachmentInventory, type BoardAttachmentInventoryQuery } from '../../shared/types/attachmentTypes';

// Authoritative snapshot for the inventory page; card editors retain their own attachment store.
export const useBoardAttachmentInventoryStore = defineStore('boardAttachmentInventory', () => {
  const api = createBoardApi();
  const feedback = useUiFeedbackStore();
  const inventory = ref<BoardAttachmentInventory | null>(null);
  const loading = ref(false);
  const error = ref('');
  const boardId = ref<number | null>(null);
  let requestVersion = 0;

  function clear() {
    requestVersion++;
    boardId.value = null;
    inventory.value = null;
    loading.value = false;
    error.value = '';
  }

  async function load(id: number, query: BoardAttachmentInventoryQuery = defaultBoardAttachmentInventoryQuery) {
    if (boardId.value !== id) { clear(); }
    boardId.value = id;
    const version = ++requestVersion;
    loading.value = true;
    error.value = '';
    const result = await api.getBoardAttachments(id, query);
    if (version !== requestVersion) { return; }
    loading.value = false;
    if (!result.ok) {
      inventory.value = null;
      error.value = result.error.message;
      feedback.setError(result.error.message);
      return;
    }
    inventory.value = result.data;
    feedback.clearError();
  }

  async function download(attachmentId: number) {
    const id = boardId.value;
    if (id === null) { return; }
    const result = await api.downloadAttachment(id, attachmentId);
    if (boardId.value !== id) { return; }
    if (!result.ok) {
      feedback.setError(`Attachment could not be downloaded: ${result.error.message}`);
      return;
    }
    feedback.clearError();
    const url = URL.createObjectURL(result.data.blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = result.data.fileName;
    link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }

  return { inventory, loading, error, load, clear, download };
});
