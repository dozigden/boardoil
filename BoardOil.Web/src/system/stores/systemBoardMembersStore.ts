import { defineStore } from 'pinia';
import { ref } from 'vue';
import { createSystemApi } from '../../shared/api/systemApi';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import type {
  BoardMember,
  BoardMemberEditModel
} from '../../shared/types/boardTypes';
import type { AppError } from '../../shared/types/appError';
import type { Result } from '../../shared/types/result';

export const useSystemBoardMembersStore = defineStore('systemBoardMembers', () => {
  const members = ref<BoardMember[]>([]);
  const busy = ref(false);
  const activeBoardId = ref<number | null>(null);
  const feedback = useUiFeedbackStore();
  const api = createSystemApi();

  function SET_MEMBERS(boardId: number, nextMembers: BoardMember[]) {
    if (activeBoardId.value !== boardId) {
      return;
    }

    members.value = [...nextMembers].sort((left, right) => left.displayName.localeCompare(right.displayName));
  }

  function UPSERT_MEMBER(boardId: number, member: BoardMember) {
    if (activeBoardId.value !== boardId) {
      return;
    }

    members.value = [...members.value.filter(existing => existing.userId !== member.userId), member]
      .sort((left, right) => left.displayName.localeCompare(right.displayName));
  }

  function REMOVE_MEMBER(boardId: number, userId: number) {
    if (activeBoardId.value !== boardId) {
      return;
    }

    members.value = members.value.filter(member => member.userId !== userId);
  }

  function dispose() {
    activeBoardId.value = null;
    members.value = [];
    busy.value = false;
  }

  async function loadMembers(boardId: number) {
    if (activeBoardId.value !== boardId) {
      members.value = [];
    }

    activeBoardId.value = boardId;
    busy.value = true;
    try {
      const result = await api.getBoardMembers(boardId);
      if (activeBoardId.value !== boardId) {
        return false;
      }

      if (!result.ok) {
        reportError(result.error);
        dispose();
        return false;
      }

      SET_MEMBERS(boardId, result.data);
      feedback.clearError();
      return true;
    } finally {
      if (activeBoardId.value === boardId) {
        busy.value = false;
      }
    }
  }

  async function addMember(boardId: number, model: BoardMemberEditModel) {
    const result = await runBusy(boardId, () => api.addBoardMember(boardId, model));
    if (!result.ok) {
      return null;
    }

    UPSERT_MEMBER(boardId, result.data);
    return result.data;
  }

  async function updateMemberRole(boardId: number, model: BoardMemberEditModel) {
    const result = await runBusy(boardId, () => api.updateBoardMemberRole(boardId, model));
    if (!result.ok) {
      return null;
    }

    UPSERT_MEMBER(boardId, result.data);
    return result.data;
  }

  async function deleteMember(boardId: number, userId: number) {
    const result = await runBusy(boardId, () => api.removeBoardMember(boardId, userId));
    if (!result.ok) {
      return false;
    }

    REMOVE_MEMBER(boardId, userId);
    return true;
  }

  async function runBusy<T>(boardId: number, operation: () => Promise<Result<T, AppError>>) {
    if (activeBoardId.value === boardId) {
      busy.value = true;
    }
    try {
      const result = await operation();
      if (activeBoardId.value !== boardId) {
        return result;
      }

      if (!result.ok) {
        reportError(result.error);
      } else {
        feedback.clearError();
      }

      return result;
    } finally {
      if (activeBoardId.value === boardId) {
        busy.value = false;
      }
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
    deleteMember
  };
});
