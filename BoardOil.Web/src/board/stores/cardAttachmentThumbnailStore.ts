import { defineStore } from 'pinia';
import { ref } from 'vue';
import { createBoardApi } from '../../shared/api/boardApi';
import type { CardAttachmentImageCandidate } from '../../shared/types/attachmentTypes';

type CandidateMap = Record<number, CardAttachmentImageCandidate>;

export const useCardAttachmentThumbnailStore = defineStore('cardAttachmentThumbnails', () => {
  const api = createBoardApi();
  const candidatesByCardId = ref<CandidateMap>({});
  const activeBoardId = ref(0);
  let requestVersion = 0;

  async function loadBoard(boardId: number, enabled: boolean) {
    const version = ++requestVersion;
    activeBoardId.value = boardId;
    candidatesByCardId.value = {};
    if (!enabled || !api.supportsAttachments) {
      return;
    }

    const result = await api.getFirstAttachmentImagesByCard(boardId);
    if (version !== requestVersion || activeBoardId.value !== boardId || !result.ok) {
      return;
    }

    candidatesByCardId.value = toCandidateMap(result.data);
  }

  function getForCard(cardId: number) {
    return candidatesByCardId.value[cardId] ?? null;
  }

  function markHasThumbnail(cardId: number, attachmentId: number) {
    const candidate = candidatesByCardId.value[cardId];
    if (!candidate || candidate.attachmentId !== attachmentId || candidate.hasThumbnail) {
      return;
    }

    candidatesByCardId.value = {
      ...candidatesByCardId.value,
      [cardId]: { ...candidate, hasThumbnail: true }
    };
  }

  function clear() {
    requestVersion++;
    activeBoardId.value = 0;
    candidatesByCardId.value = {};
  }

  return {
    candidatesByCardId,
    activeBoardId,
    loadBoard,
    getForCard,
    markHasThumbnail,
    clear
  };
});

function toCandidateMap(candidates: CardAttachmentImageCandidate[]) {
  const mapped: CandidateMap = {};
  for (const candidate of candidates) {
    mapped[candidate.cardId] = { ...candidate };
  }
  return mapped;
}
