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
  const thumbnailImageUrls = new Map<number, string>();

  function SET_CANDIDATES(boardId: number, candidates: CardAttachmentImageCandidate[]) {
    if (!canHandle(boardId)) {
      return;
    }

    candidatesByCardId.value = toCandidateMap(candidates);
  }

  function APPLY_CARD_REFRESH(
    boardId: number,
    cardIds: number[],
    candidates: CardAttachmentImageCandidate[]
  ) {
    if (!canHandle(boardId)) {
      return;
    }

    const refreshedCandidates = toCandidateMap(candidates);
    const nextCandidates = { ...candidatesByCardId.value };
    for (const cardId of cardIds) {
      const candidate = refreshedCandidates[cardId];
      if (candidate) {
        nextCandidates[cardId] = candidate;
      } else {
        delete nextCandidates[cardId];
      }
    }

    candidatesByCardId.value = nextCandidates;
  }

  function ADD_CANDIDATE(boardId: number, cardId: number, attachment: CardAttachment) {
    if (!canHandle(boardId) || !isSupportedImageFileName(attachment.originalFileName)) {
      return;
    }

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

  function REMOVE_CANDIDATE(boardId: number, cardId: number, attachmentId?: number) {
    if (!canHandle(boardId)) {
      return;
    }

    const candidate = candidatesByCardId.value[cardId];
    if (attachmentId !== undefined && (!candidate || candidate.attachmentId !== attachmentId)) {
      return;
    }

    if (!candidate) {
      return;
    }

    const nextCandidates = { ...candidatesByCardId.value };
    delete nextCandidates[cardId];
    candidatesByCardId.value = nextCandidates;
  }

  function CLEAR_THUMBNAIL_IMAGES() {
    for (const imageUrl of thumbnailImageUrls.values()) {
      URL.revokeObjectURL(imageUrl);
    }
    thumbnailImageUrls.clear();
  }

  function MARK_HAS_THUMBNAIL(cardId: number, attachmentId: number) {
    const candidate = candidatesByCardId.value[cardId];
    if (!candidate || candidate.attachmentId !== attachmentId || candidate.hasThumbnail) {
      return;
    }

    candidatesByCardId.value = {
      ...candidatesByCardId.value,
      [cardId]: { ...candidate, hasThumbnail: true }
    };
  }

  function SET_ENABLED(value: boolean) {
    enabled.value = value && api.supportsAttachments;
  }

  async function loadBoard(boardId: number, thumbnailsEnabled: boolean) {
    if (activeBoardId.value !== boardId) {
      CLEAR_THUMBNAIL_IMAGES();
    }
    activeBoardId.value = boardId;
    SET_ENABLED(thumbnailsEnabled);
    candidatesByCardId.value = {};
    if (!thumbnailsEnabled || !api.supportsAttachments) {
      return;
    }

    const result = await api.getCardThumbnails(boardId);
    if (!result.ok) {
      return;
    }

    SET_CANDIDATES(boardId, result.data);
  }

  async function refreshCards(boardId: number, cardIds: number[]) {
    if (!canHandle(boardId)) {
      return;
    }

    const uniqueCardIds = [...new Set(cardIds)].filter(cardId => cardId > 0);
    if (uniqueCardIds.length === 0) {
      return;
    }

    const result = await api.getCardThumbnails(boardId, uniqueCardIds);
    if (!result.ok) {
      return;
    }

    APPLY_CARD_REFRESH(boardId, uniqueCardIds, result.data);
  }

  function attachmentAdded(boardId: number, cardId: number, attachment: CardAttachment) {
    ADD_CANDIDATE(boardId, cardId, attachment);
  }

  async function attachmentDeleted(boardId: number, cardId: number, attachmentId: number) {
    if (activeBoardId.value === boardId) {
      const imageUrl = thumbnailImageUrls.get(attachmentId);
      if (imageUrl) {
        URL.revokeObjectURL(imageUrl);
        thumbnailImageUrls.delete(attachmentId);
      }
    }
    REMOVE_CANDIDATE(boardId, cardId, attachmentId);
    await refreshCards(boardId, [cardId]);
  }

  function cardRemoved(boardId: number, cardId: number) {
    REMOVE_CANDIDATE(boardId, cardId);
  }

  function getForCard(cardId: number) {
    return candidatesByCardId.value[cardId] ?? null;
  }

  function getThumbnailImageUrl(boardId: number, attachmentId: number) {
    if (activeBoardId.value !== boardId) {
      return null;
    }
    return thumbnailImageUrls.get(attachmentId) ?? null;
  }

  function cacheThumbnailImage(boardId: number, attachmentId: number, image: Blob) {
    if (activeBoardId.value !== boardId) {
      return null;
    }
    const cached = thumbnailImageUrls.get(attachmentId);
    if (cached) {
      return cached;
    }
    const imageUrl = URL.createObjectURL(image);
    thumbnailImageUrls.set(attachmentId, imageUrl);
    return imageUrl;
  }

  function markHasThumbnail(cardId: number, attachmentId: number) {
    MARK_HAS_THUMBNAIL(cardId, attachmentId);
  }

  function clear() {
    activeBoardId.value = 0;
    enabled.value = false;
    candidatesByCardId.value = {};
    CLEAR_THUMBNAIL_IMAGES();
  }

  function canHandle(boardId: number) {
    return enabled.value && activeBoardId.value === boardId;
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
    getThumbnailImageUrl,
    cacheThumbnailImage,
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
