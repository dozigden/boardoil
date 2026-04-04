import type { BoardMember, BoardMemberRole, SystemBoardSummary } from '../types/boardTypes';
import type { AppError } from '../types/appError';
import type { Result } from '../types/result';
import { ok } from '../types/result';
import { deleteJson, getEnvelope, patchData, postData } from './http';

export type SystemBoardApi = ReturnType<typeof createSystemBoardApi>;

export function createSystemBoardApi() {
  async function getBoards(): Promise<Result<SystemBoardSummary[], AppError>> {
    const envelopeResult = await getEnvelope<SystemBoardSummary[]>('/api/admin/boards');
    if (!envelopeResult.ok) {
      return envelopeResult;
    }

    return ok(envelopeResult.data.data ?? []);
  }

  async function getBoardMembers(boardId: number): Promise<Result<BoardMember[], AppError>> {
    const envelopeResult = await getEnvelope<BoardMember[]>(`/api/admin/boards/${boardId}/members`);
    if (!envelopeResult.ok) {
      return envelopeResult;
    }

    return ok(envelopeResult.data.data ?? []);
  }

  async function addBoardMember(boardId: number, userId: number, role: BoardMemberRole): Promise<Result<BoardMember, AppError>> {
    return postData<BoardMember>(`/api/admin/boards/${boardId}/members`, { userId, role });
  }

  async function updateBoardMemberRole(
    boardId: number,
    userId: number,
    role: BoardMemberRole
  ): Promise<Result<BoardMember, AppError>> {
    return patchData<BoardMember>(`/api/admin/boards/${boardId}/members/${userId}`, { role });
  }

  async function removeBoardMember(boardId: number, userId: number): Promise<Result<void, AppError>> {
    return deleteJson(`/api/admin/boards/${boardId}/members/${userId}`);
  }

  return {
    getBoards,
    getBoardMembers,
    addBoardMember,
    updateBoardMemberRole,
    removeBoardMember
  };
}

export const systemBoardApi = createSystemBoardApi();
