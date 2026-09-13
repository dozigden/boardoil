import { defineStore } from 'pinia';
import { ref } from 'vue';
import { createBoardApi } from '../../shared/api/boardApi';
import type { CardAttachment, CardAttachmentImageCandidate } from '../../shared/types/attachmentTypes';
import { isSupportedImageFileName } from '../../shared/components/markdownImages';

type CandidateMap = Record<number, CardAttachmentImageCandidate>;

export const useCardAttachmentThumbnailStore = defineStore('cardAttachmentThumbnails', () => {
  const api = createBoardApi();
  const candidatesByCardId = ref<CandidateMap>({});
  const activeBoardId = ref(0);
  const enabled = ref(false);
  let requestVersion = 0;
  let mutationVersion = 0;
  const cardRefreshVersions = new Map<number, number>();

  async function loadBoard(boardId: number, thumbnailsEnabled: boolean) {
    const version = ++requestVersion;
    activeBoardId.value = boardId;
    setEnabled(thumbnailsEnabled);
    candidatesByCardId.value = {};
    cardRefreshVersions.clear();
    if (!thumbnailsEnabled || !api.supportsAttachments) {
      return;
    }

    const mutationsAtStart = mutationVersion;
    const result = await api.getFirstAttachmentImagesByCard(boardId);
    if (version !== requestVersion || activeBoardId.value !== boardId || !result.ok) {
      return;
    }
    if (mutationsAtStart !== mutationVersion) {
      await loadBoard(boardId, true);
      return;
    }

    candidatesByCardId.value = toCandidateMap(result.data);
  }

  async function refreshCards(boardId: number, cardIds: number[]) {
    if (!canHandle(boardId)) {
      return;
    }

    const uniqueCardIds = [...new Set(cardIds)].filter(cardId => cardId > 0);
    if (uniqueCardIds.length === 0) {
      return;
    }

    mutationVersion++;
    const refreshVersions = new Map<number, number>();
    const attachmentIdsAtStart = new Map<number, number | null>();
    for (const cardId of uniqueCardIds) {
      refreshVersions.set(cardId, advanceCardRefreshVersion(cardId));
      attachmentIdsAtStart.set(cardId, candidatesByCardId.value[cardId]?.attachmentId ?? null);
    }

    const result = await api.getFirstAttachmentImagesByCard(boardId, uniqueCardIds);
    if (!canHandle(boardId) || !result.ok) {
      return;
    }

    const refreshedCandidates = toCandidateMap(result.data);
    const nextCandidates = { ...candidatesByCardId.value };
    for (const cardId of uniqueCardIds) {
      if (cardRefreshVersions.get(cardId) !== refreshVersions.get(cardId)) {
        continue;
      }

      const candidate = refreshedCandidates[cardId];
      if (candidate) {
        const currentCandidate = candidatesByCardId.value[cardId];
        nextCandidates[cardId] = currentCandidate?.attachmentId === candidate.attachmentId && currentCandidate.hasThumbnail
          ? { ...candidate, hasThumbnail: true }
          : candidate;
      } else if (candidatesByCardId.value[cardId]?.attachmentId !== attachmentIdsAtStart.get(cardId)) {
        continue;
      } else {
        delete nextCandidates[cardId];
      }
    }

    candidatesByCardId.value = nextCandidates;
  }

  function attachmentAdded(boardId: number, cardId: number, attachment: CardAttachment) {
    if (!canHandle(boardId) || !isSupportedImageFileName(attachment.originalFileName)) {
      return;
    }

    mutationVersion++;
    if (candidatesByCardId.value[cardId]) {
      return;
    }

    candidatesByCardId.value = {
      ...candidatesByCardId.value,
      [cardId]: {
        cardId,
        attachmentId: attachment.id,
        originalFileName: attachment.originalFileName,
        hasThumbnail: attachment.hasThumbnail
      }
    };
  }

  async function attachmentDeleted(boardId: number, cardId: number, attachmentId: number) {
    if (!canHandle(boardId)) {
      return;
    }

    const candidate = candidatesByCardId.value[cardId];
    if (!candidate || candidate.attachmentId !== attachmentId) {
      return;
    }

    mutationVersion++;
    advanceCardRefreshVersion(cardId);
    const nextCandidates = { ...candidatesByCardId.value };
    delete nextCandidates[cardId];
    candidatesByCardId.value = nextCandidates;
    await refreshCards(boardId, [cardId]);
  }

  function cardRemoved(boardId: number, cardId: number) {
    if (!canHandle(boardId)) {
      return;
    }

    mutationVersion++;
    advanceCardRefreshVersion(cardId);
    if (!candidatesByCardId.value[cardId]) {
      return;
    }

    const nextCandidates = { ...candidatesByCardId.value };
    delete nextCandidates[cardId];
    candidatesByCardId.value = nextCandidates;
  }

  function getForCard(cardId: number) {
    return candidatesByCardId.value[cardId] ?? null;
  }

  function markHasThumbnail(cardId: number, attachmentId: number) {
    const candidate = candidatesByCardId.value[cardId];
    if (!candidate || candidate.attachmentId !== attachmentId || candidate.hasThumbnail) {
      return;
    }

    mutationVersion++;
    candidatesByCardId.value = {
      ...candidatesByCardId.value,
      [cardId]: { ...candidate, hasThumbnail: true }
    };
  }

  function clear() {
    requestVersion++;
    mutationVersion++;
    activeBoardId.value = 0;
    enabled.value = false;
    candidatesByCardId.value = {};
    cardRefreshVersions.clear();
  }

  function setEnabled(value: boolean) {
    enabled.value = value && api.supportsAttachments;
  }

  function canHandle(boardId: number) {
    return enabled.value && activeBoardId.value === boardId;
  }

  function advanceCardRefreshVersion(cardId: number) {
    const nextVersion = (cardRefreshVersions.get(cardId) ?? 0) + 1;
    cardRefreshVersions.set(cardId, nextVersion);
    return nextVersion;
  }

  return {
    candidatesByCardId,
    activeBoardId,
    loadBoard,
    refreshCards,
    attachmentAdded,
    attachmentDeleted,
    cardRemoved,
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
