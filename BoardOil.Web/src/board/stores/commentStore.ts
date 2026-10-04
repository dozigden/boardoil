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
  // Keep confirmed deletions across same-board resyncs so delayed reads/writes cannot restore them.
  const deletedCommentIds = ref(new Set<number>());
  let commentIdsBeforeResync: Record<number, number[]> = {};
  const feedback = useUiFeedbackStore();
  const api = createBoardApi();

  function SET_CARD_COMMENTS(boardId: number, cardId: number, comments: CardComment[], removedIds: number[] = []) {
    if (activeBoardId.value !== boardId) {
      return;
    }

    for (const commentId of removedIds) {
      deletedCommentIds.value.add(commentId);
    }
    commentsByCardId.value = {
      ...commentsByCardId.value,
      [cardId]: normalizeComments(comments.filter(comment => !deletedCommentIds.value.has(comment.id)))
    };
  }

  function UPSERT_CARD_COMMENT(boardId: number, comment: CardComment) {
    if (activeBoardId.value !== boardId || deletedCommentIds.value.has(comment.id)) {
      return false;
    }

    const existingComments = commentsByCardId.value[comment.cardId] ?? [];
    const withoutExisting = existingComments.filter(existing => existing.id !== comment.id);
    SET_CARD_COMMENTS(boardId, comment.cardId, [comment, ...withoutExisting]);
    return true;
  }

  function REMOVE_CARD_COMMENTS(boardId: number, cardId: number, commentIds: number[]) {
    if (activeBoardId.value !== boardId) {
      return false;
    }
    SET_CARD_COMMENTS(boardId, cardId, commentsByCardId.value[cardId] ?? [], commentIds);
    return true;
  }

  function initialize(boardId: number) {
    if (activeBoardId.value === boardId) {
      for (const [cardId, comments] of Object.entries(commentsByCardId.value)) {
        const id = Number(cardId);
        commentIdsBeforeResync[id] = [...new Set([...(commentIdsBeforeResync[id] ?? []), ...comments.map(comment => comment.id)])];
      }
      commentsByCardId.value = {};
      busy.value = false;
      return;
    }
    dispose();
    activeBoardId.value = boardId;
  }

  function dispose() {
    activeBoardId.value = 0;
    commentsByCardId.value = {};
    commentIdsBeforeResync = {};
    deletedCommentIds.value = new Set<number>();
    busy.value = false;
  }

  async function loadCardComments(boardId: number, cardId: number) {
    const previouslyLoadedIds = new Set([
      ...(commentIdsBeforeResync[cardId] ?? []),
      ...(commentsByCardId.value[cardId] ?? []).map(comment => comment.id)
    ]);
    const result = await runBusy(() => api.getCardComments(boardId, cardId), boardId);
    if (!result.ok) {
      return result;
    }

    if (activeBoardId.value === boardId) {
      const returnedIds = new Set(result.data.map(comment => comment.id));
      const missingIds = [...previouslyLoadedIds].filter(id => !returnedIds.has(id));
      delete commentIdsBeforeResync[cardId];
      SET_CARD_COMMENTS(boardId, cardId, result.data, missingIds);
    }

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

  async function updateCardComment(boardId: number, cardId: number, commentId: number, text: string) {
    const result = await runBusy(() => api.updateCardComment(boardId, cardId, commentId, text), boardId);
    if (!result.ok) {
      return result;
    }
    if (!UPSERT_CARD_COMMENT(boardId, result.data)) {
      return null;
    }
    return result;
  }

  function upsertCardComment(boardId: number, comment: CardComment) {
    UPSERT_CARD_COMMENT(boardId, comment);
  }

  async function deleteCardComment(boardId: number, cardId: number, commentId: number) {
    const result = await runBusy(() => api.deleteCardComment(boardId, cardId, commentId), boardId);
    if (!result.ok) {
      return result;
    }
    if (!REMOVE_CARD_COMMENTS(boardId, cardId, [commentId])) {
      return null;
    }
    return result;
  }

  function removeCardComment(boardId: number, cardId: number, commentId: number) {
    REMOVE_CARD_COMMENTS(boardId, cardId, [commentId]);
  }

  function isCommentDeleted(commentId: number) {
    return deletedCommentIds.value.has(commentId);
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
    updateCardComment,
    deleteCardComment,
    removeCardComment,
    isCommentDeleted,
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
