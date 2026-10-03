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

  function SET_CARD_COMMENTS(boardId: number, cardId: number, comments: CardComment[]) {
    if (activeBoardId.value !== boardId) {
      return;
    }

    commentsByCardId.value = {
      ...commentsByCardId.value,
      [cardId]: normalizeComments(comments)
    };
  }

  function UPSERT_CARD_COMMENT(boardId: number, comment: CardComment) {
    if (activeBoardId.value !== boardId) {
      return false;
    }

    const existingComments = commentsByCardId.value[comment.cardId] ?? [];
    const withoutExisting = existingComments.filter(existing => existing.id !== comment.id);
    SET_CARD_COMMENTS(boardId, comment.cardId, [comment, ...withoutExisting]);
    return true;
  }

  function initialize(boardId: number) {
    dispose();
    activeBoardId.value = boardId;
  }

  function dispose() {
    activeBoardId.value = 0;
    commentsByCardId.value = {};
    busy.value = false;
  }

  async function loadCardComments(boardId: number, cardId: number) {
    const result = await runBusy(() => api.getCardComments(boardId, cardId), boardId);
    if (!result.ok) {
      return result;
    }

    SET_CARD_COMMENTS(boardId, cardId, result.data);

    return result;
  }

  async function addCardComment(boardId: number, cardId: number, text: string) {
    const result = await runBusy(() => api.createCardComment(boardId, cardId, text), boardId);
    if (!result.ok) {
      return result;
    }

    if (!UPSERT_CARD_COMMENT(boardId, result.data)) {
      return null;
    }

    return result;
  }

  function getCommentsForCard(cardId: number | null) {
    if (cardId === null) {
      return [];
    }

    return commentsByCardId.value[cardId] ?? [];
  }

  function upsertCardComment(boardId: number, comment: CardComment) {
    UPSERT_CARD_COMMENT(boardId, comment);
  }

  async function runBusy<T>(operation: () => Promise<Result<T, AppError>>, boardId: number) {
    if (activeBoardId.value === boardId) {
      busy.value = true;
    }
    try {
      const result = await operation();
      if (activeBoardId.value !== boardId) {
        return result;
      }

      if (!result.ok) {
        feedback.setError(result.error.message, 'comment');
      } else {
        feedback.clearError('comment');
      }

      return result;
    } finally {
      if (activeBoardId.value === boardId) {
        busy.value = false;
      }
    }
  }

  return {
    commentsByCardId,
    busy,
    initialize,
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
  if (left.postedAtUtc > right.postedAtUtc) {
    return -1;
  }

  if (left.postedAtUtc < right.postedAtUtc) {
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
