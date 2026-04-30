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
  const feedback = useUiFeedbackStore();
  const api = createBoardApi();

  function dispose() {
    commentsByCardId.value = {};
    busy.value = false;
  }

  async function loadCardComments(boardId: number, cardId: number) {
    const result = await runBusy(() => api.getCardComments(boardId, cardId));
    if (!result.ok) {
      return result;
    }

    setCardComments(cardId, result.data);
    return result;
  }

  async function addCardComment(boardId: number, cardId: number, text: string) {
    const result = await runBusy(() => api.createCardComment(boardId, cardId, text));
    if (!result.ok) {
      return result;
    }

    const existingComments = commentsByCardId.value[cardId] ?? [];
    setCardComments(cardId, [result.data, ...existingComments]);
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
      [cardId]: comments.map(comment => ({ ...comment }))
    };
  }

  async function runBusy<T>(operation: () => Promise<Result<T, AppError>>) {
    busy.value = true;
    try {
      const result = await operation();
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
    getCommentsForCard
  };
});
