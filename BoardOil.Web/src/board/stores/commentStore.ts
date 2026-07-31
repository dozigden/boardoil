import { defineStore } from 'pinia';
import { ref } from 'vue';
import { createBoardApi } from '../../shared/api/boardApi';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import type { CardComment } from '../../shared/types/boardTypes';
import type { AppError } from '../../shared/types/appError';
import type { Result } from '../../shared/types/result';

type CommentsByCardIdMap = Record<number, CardComment[]>;

export const useCommentStore = defineStore('comment', () => {
  const commentsByCardId = ref<CommentsByCardIdMap>({});
  const busy = ref(false);
  const activeBoardId = ref(0);
  const feedback = useUiFeedbackStore();
  const api = createBoardApi();

  function dispose() {
    activeBoardId.value = 0;
    commentsByCardId.value = {};
    busy.value = false;
  }

  async function loadCardComments(boardId: number, cardId: number) {
    activeBoardId.value = boardId;
    const result = await runBusy(() => api.getCardComments(boardId, cardId), boardId);
    if (!result.ok) {
      return result;
    }

    if (activeBoardId.value === boardId) {
      setCardComments(cardId, result.data);
    }

    return result;
  }

  async function addCardComment(boardId: number, cardId: number, text: string) {
    activeBoardId.value = boardId;
    const result = await runBusy(() => api.createCardComment(boardId, cardId, text), boardId);
    if (!result.ok) {
      return result;
    }

    if (activeBoardId.value !== boardId) {
      return null;
    }

    upsertCardComment(result.data);
    return result;
  }

  function getCommentsForCard(cardId: number | null) {
    if (cardId === null) {
      return [];
    }

    return commentsByCardId.value[cardId] ?? [];
  }

  function setCardComments(cardId: number, comments: CardComment[]) {
    commentsByCardId.value = {
      ...commentsByCardId.value,
      [cardId]: normalizeComments(comments)
    };
  }

  function upsertCardComment(comment: CardComment) {
    const existingComments = commentsByCardId.value[comment.cardId] ?? [];
    const withoutExisting = existingComments.filter(existing => existing.id !== comment.id);
    setCardComments(comment.cardId, [comment, ...withoutExisting]);
  }

  async function runBusy<T>(operation: () => Promise<Result<T, AppError>>, boardId: number) {
    busy.value = true;
    try {
      const result = await operation();
      if (activeBoardId.value !== boardId) {
        return result;
      }

      if (!result.ok) {
        feedback.setError(result.error.message);
      } else {
        feedback.clearError();
      }

      return result;
    } finally {
      busy.value = false;
    }
  }

  return {
    commentsByCardId,
    busy,
    dispose,
    loadCardComments,
    addCardComment,
    getCommentsForCard,
    upsertCardComment
  };
});

function normalizeComments(comments: CardComment[]) {
  return comments
    .map(comment => ({ ...comment }))
    .sort(compareCommentsDescending);
}

function compareCommentsDescending(left: CardComment, right: CardComment) {
  if (left.createdAtUtc > right.createdAtUtc) {
    return -1;
  }

  if (left.createdAtUtc < right.createdAtUtc) {
    return 1;
  }

  if (left.id > right.id) {
    return -1;
  }

  if (left.id < right.id) {
    return 1;
  }

  return 0;
}
