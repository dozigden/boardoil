import { defineStore } from 'pinia';
import { ref } from 'vue';
import { createSystemApi } from '../api/systemApi';
import { useUiFeedbackStore } from './uiFeedbackStore';
import type { BoardMember, BoardMemberRole } from '../types/boardTypes';
import type { AppError } from '../types/appError';
import type { Result } from '../types/result';

export const useSystemBoardMembersStore = defineStore('systemBoardMembers', () => {
  const members = ref<BoardMember[]>([]);
  const busy = ref(false);
  const activeBoardId = ref<number | null>(null);
  const feedback = useUiFeedbackStore();
  const api = createSystemApi();

  function dispose() {
    activeBoardId.value = null;
    members.value = [];
    busy.value = false;
  }

  async function loadMembers(boardId: number | null = activeBoardId.value) {
    const resolvedBoardId = resolveBoardId(boardId);
    if (resolvedBoardId === null) {
      members.value = [];
      return false;
    }

    activeBoardId.value = resolvedBoardId;
    busy.value = true;
    try {
      const result = await api.getBoardMembers(resolvedBoardId);
      if (!result.ok) {
        reportError(result.error);
        members.value = [];
        return false;
      }

      members.value = [...result.data].sort((left, right) => left.userName.localeCompare(right.userName));
      feedback.clearError();
      return true;
    } finally {
      busy.value = false;
    }
  }

  async function addMember(
    userId: number,
    role: BoardMemberRole,
    boardId: number | null = activeBoardId.value
  ) {
    const resolvedBoardId = resolveBoardId(boardId);
    if (resolvedBoardId === null) {
      return null;
    }

    const result = await runBusy(() => api.addBoardMember(resolvedBoardId, userId, role));
    if (!result.ok) {
      return null;
    }

    await loadMembers(resolvedBoardId);
    return result.data;
  }

  async function updateMemberRole(
    userId: number,
    role: BoardMemberRole,
    boardId: number | null = activeBoardId.value
  ) {
    const resolvedBoardId = resolveBoardId(boardId);
    if (resolvedBoardId === null) {
      return null;
    }

    const result = await runBusy(() => api.updateBoardMemberRole(resolvedBoardId, userId, role));
    if (!result.ok) {
      await loadMembers(resolvedBoardId);
      return null;
    }

    await loadMembers(resolvedBoardId);
    return result.data;
  }

  async function removeMember(userId: number, boardId: number | null = activeBoardId.value) {
    const resolvedBoardId = resolveBoardId(boardId);
    if (resolvedBoardId === null) {
      return false;
    }

    const result = await runBusy(() => api.removeBoardMember(resolvedBoardId, userId));
    if (!result.ok) {
      return false;
    }

    await loadMembers(resolvedBoardId);
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

  function resolveBoardId(boardId: number | null) {
    const resolved = boardId ?? activeBoardId.value;
    if (resolved === null) {
      feedback.setError('No board selected.');
      return null;
    }

    return resolved;
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
