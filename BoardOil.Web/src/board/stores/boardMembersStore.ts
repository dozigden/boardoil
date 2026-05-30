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
  const activeBoardId = ref<number | null>(null);
  const feedback = useUiFeedbackStore();
  const api = createBoardApi();
  let loadRequestVersion = 0;

  function dispose() {
    loadRequestVersion += 1;
    activeBoardId.value = null;
    members.value = [];
    busy.value = false;
  }

  async function loadMembers(boardId: number) {
    const requestVersion = ++loadRequestVersion;
    if (activeBoardId.value !== boardId) {
      members.value = [];
    }

    activeBoardId.value = boardId;
    busy.value = true;
    try {
      const result = await api.getBoardMembers(boardId);
      if (requestVersion !== loadRequestVersion) {
        return false;
      }

      if (!result.ok) {
        reportError(result.error);
        members.value = [];
        return false;
      }

      members.value = [...result.data].sort((left, right) => left.displayName.localeCompare(right.displayName));
      feedback.clearError();
      return true;
    } finally {
      if (requestVersion === loadRequestVersion) {
        busy.value = false;
      }
    }
  }

  async function addMember(boardId: number, model: BoardMemberEditModel) {
    const result = await runBusy(() => api.addBoardMember(boardId, model));
    if (!result.ok) {
      return null;
    }

    await loadMembers(boardId);
    return result.data;
  }

  async function updateMemberRole(boardId: number, model: BoardMemberEditModel) {
    const result = await runBusy(() => api.updateBoardMemberRole(boardId, model));
    if (!result.ok) {
      await loadMembers(boardId);
      return null;
    }

    await loadMembers(boardId);
    return result.data;
  }

  async function removeMember(boardId: number, userId: number) {
    const result = await runBusy(() => api.removeBoardMember(boardId, userId));
    if (!result.ok) {
      return false;
    }

    await loadMembers(boardId);
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
