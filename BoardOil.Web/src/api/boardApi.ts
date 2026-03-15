import type { Board, Card, Column } from '../types/boardTypes';
import type { AppError } from '../types/appError';
import type { Result } from '../types/result';
import { err, ok } from '../types/result';
import { deleteJson, getEnvelope, patchData, postData } from './http';

export type BoardApi = ReturnType<typeof createBoardApi>;

export function createBoardApi() {
  async function getBoard(): Promise<Result<Board, AppError>> {
    const envelopeResult = await getEnvelope<Board>('/api/board');
    if (!envelopeResult.ok) {
      return envelopeResult;
    }

    if (!envelopeResult.data.data) {
      return err({
        kind: 'api',
        message: envelopeResult.data.message ?? 'Failed to load board.'
      });
    }

    return ok(envelopeResult.data.data);
  }

  async function createColumn(title: string): Promise<Result<Column, AppError>> {
    return postData<Column>('/api/columns', { title, position: null });
  }

  async function saveColumn(
    columnId: number,
    title: string
  ): Promise<Result<Column, AppError>> {
    return patchData<Column>(`/api/columns/${columnId}`, { title, position: null });
  }

  async function moveColumn(columnId: number, position: number): Promise<Result<Column, AppError>> {
    return patchData<Column>(`/api/columns/${columnId}`, { title: null, position });
  }

  async function deleteColumn(columnId: number): Promise<Result<void, AppError>> {
    return deleteJson(`/api/columns/${columnId}`);
  }

  async function createCard(columnId: number, title: string): Promise<Result<Card, AppError>> {
    return postData<Card>('/api/cards', {
      boardColumnId: columnId,
      title,
      description: '',
      position: null
    });
  }

  async function saveCard(
    cardId: number,
    title: string,
    description: string
  ): Promise<Result<Card, AppError>> {
    return patchData<Card>(`/api/cards/${cardId}`, {
      boardColumnId: null,
      title,
      description,
      position: null
    });
  }

  async function moveCard(
    cardId: number,
    boardColumnId: number,
    position: number
  ): Promise<Result<Card, AppError>> {
    return patchData<Card>(`/api/cards/${cardId}`, {
      boardColumnId,
      title: null,
      description: null,
      position
    });
  }

  async function deleteCard(cardId: number): Promise<Result<void, AppError>> {
    return deleteJson(`/api/cards/${cardId}`);
  }

  return {
    getBoard,
    createColumn,
    saveColumn,
    moveColumn,
    deleteColumn,
    createCard,
    saveCard,
    moveCard,
    deleteCard
  };
}

export const boardApi = createBoardApi();
