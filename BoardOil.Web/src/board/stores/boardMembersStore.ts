import { defineStore } from 'pinia';
import { ref } from 'vue';
import { createBoardApi } from '../../shared/api/boardApi';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import type {
  BoardMember,
  BoardMemberEditModel
} from '../../shared/types/boardTypes';
import type { AppError } from '../../shared/types/appError';
import type { Result } from '../../shared/types/result';

export const useBoardMembersStore = defineStore('boardMembers', () => {
  const members = ref<BoardMember[]>([]);
  const busy = ref(false);
  const activeBoardId = ref(0);
  const feedback = useUiFeedbackStore();
  const api = createBoardApi();

  function dispose() {
    activeBoardId.value = 0;
    members.value = [];
    busy.value = false;
  }

  async function loadMembers(boardId: number) {
    activeBoardId.value = boardId;
    busy.value = true;
    try {
      const result = await api.getBoardMembers(boardId);
      if (!result.ok) {
        reportError(result.error);
        members.value = [];
        return false;
      }

      members.value = [...result.data].sort((left, right) => left.displayName.localeCompare(right.displayName));
      feedback.clearError();
      return true;
    } finally {
      busy.value = false;
    }
  }

  async function addMember(model: BoardMemberEditModel) {
    const result = await runBusy(() => api.addBoardMember(activeBoardId.value, model));
    if (!result.ok) {
      return null;
    }

    await loadMembers(activeBoardId.value);
    return result.data;
  }

  async function updateMemberRole(model: BoardMemberEditModel) {
    const result = await runBusy(() => api.updateBoardMemberRole(activeBoardId.value, model));
    if (!result.ok) {
      await loadMembers(activeBoardId.value);
      return null;
    }

    await loadMembers(activeBoardId.value);
    return result.data;
  }

  async function removeMember(userId: number) {
    const result = await runBusy(() => api.removeBoardMember(activeBoardId.value, userId));
    if (!result.ok) {
      return false;
    }

    await loadMembers(activeBoardId.value);
    return true;
  }

  async function runBusy<T>(operation: () => Promise<Result<T, AppError>>) {
    busy.value = true;
    try {
      const result = await operation();
      if (!result.ok) {
        reportError(result.error);
      } else {
        feedback.clearError();
      }

      return result;
    } finally {
      busy.value = false;
    }
  }

  function reportError(error: AppError) {
    feedback.setError(error.message);
  }

  return {
    members,
    busy,
    activeBoardId,
    dispose,
    loadMembers,
    addMember,
    updateMemberRole,
    removeMember
  };
});
