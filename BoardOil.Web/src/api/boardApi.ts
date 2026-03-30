import type { Board, BoardSummary, Card, Column, Tag, TagStyleName } from '../types/boardTypes';
import type { AppError } from '../types/appError';
import type { Result } from '../types/result';
import { err, ok } from '../types/result';
import { deleteJson, getEnvelope, patchData, postData, putData } from './http';

export type BoardApi = ReturnType<typeof createBoardApi>;

export function createBoardApi() {
  async function getBoards(): Promise<Result<BoardSummary[], AppError>> {
    const envelopeResult = await getEnvelope<BoardSummary[]>('/api/boards');
    if (!envelopeResult.ok) {
      return envelopeResult;
    }

    return ok(envelopeResult.data.data ?? []);
  }

  async function getBoard(boardId: number): Promise<Result<Board, AppError>> {
    const envelopeResult = await getEnvelope<Board>(`/api/boards/${boardId}`);
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

  async function createBoard(name: string): Promise<Result<Board, AppError>> {
    return postData<Board>('/api/boards', { name });
  }

  async function saveBoard(boardId: number, name: string): Promise<Result<BoardSummary, AppError>> {
    return putData<BoardSummary>(`/api/boards/${boardId}`, { name });
  }

  async function deleteBoard(boardId: number): Promise<Result<void, AppError>> {
    return deleteJson(`/api/boards/${boardId}`);
  }

  async function getColumns(boardId: number): Promise<Result<Column[], AppError>> {
    const envelopeResult = await getEnvelope<Column[]>(`/api/boards/${boardId}/columns`);
    if (!envelopeResult.ok) {
      return envelopeResult;
    }

    return ok(envelopeResult.data.data ?? []);
  }

  async function createColumn(boardId: number, title: string): Promise<Result<Column, AppError>> {
    return postData<Column>(`/api/boards/${boardId}/columns`, { title });
  }

  async function saveColumn(
    boardId: number,
    columnId: number,
    title: string
  ): Promise<Result<Column, AppError>> {
    return putData<Column>(`/api/boards/${boardId}/columns/${columnId}`, { title });
  }

  async function moveColumn(boardId: number, columnId: number, positionAfterColumnId: number | null): Promise<Result<Column, AppError>> {
    return patchData<Column>(`/api/boards/${boardId}/columns/${columnId}/move`, { positionAfterColumnId });
  }

  async function deleteColumn(boardId: number, columnId: number): Promise<Result<void, AppError>> {
    return deleteJson(`/api/boards/${boardId}/columns/${columnId}`);
  }

  async function createCard(boardId: number, columnId: number, title: string): Promise<Result<Card, AppError>> {
    return postData<Card>(`/api/boards/${boardId}/cards`, {
      boardColumnId: columnId,
      title,
      description: '',
      tagNames: []
    });
  }

  async function saveCard(
    boardId: number,
    cardId: number,
    title: string,
    description: string,
    tagNames: string[]
  ): Promise<Result<Card, AppError>> {
    return putData<Card>(`/api/boards/${boardId}/cards/${cardId}`, {
      title,
      description,
      tagNames
    });
  }

  async function moveCard(
    boardId: number,
    cardId: number,
    boardColumnId: number,
    positionAfterCardId: number | null
  ): Promise<Result<Card, AppError>> {
    return patchData<Card>(`/api/boards/${boardId}/cards/${cardId}/move`, {
      boardColumnId,
      positionAfterCardId
    });
  }

  async function deleteCard(boardId: number, cardId: number): Promise<Result<void, AppError>> {
    return deleteJson(`/api/boards/${boardId}/cards/${cardId}`);
  }

  async function getTags(boardId: number): Promise<Result<Tag[], AppError>> {
    const envelopeResult = await getEnvelope<Tag[]>(`/api/boards/${boardId}/tags`);
    if (!envelopeResult.ok) {
      return envelopeResult;
    }

    return ok(envelopeResult.data.data ?? []);
  }

  async function createTag(boardId: number, name: string): Promise<Result<Tag, AppError>> {
    return postData<Tag>(`/api/boards/${boardId}/tags`, { name });
  }

  async function updateTagStyle(
    boardId: number,
    tagId: number,
    styleName: TagStyleName,
    stylePropertiesJson: string
  ): Promise<Result<Tag, AppError>> {
    return patchData<Tag>(`/api/boards/${boardId}/tags/${tagId}`, {
      styleName,
      stylePropertiesJson
    });
  }

  async function deleteTag(boardId: number, tagId: number): Promise<Result<void, AppError>> {
    return deleteJson(`/api/boards/${boardId}/tags/${tagId}`);
  }

  return {
    getBoards,
    getBoard,
    createBoard,
    saveBoard,
    deleteBoard,
    getColumns,
    createColumn,
    saveColumn,
    moveColumn,
    deleteColumn,
    createCard,
    saveCard,
    moveCard,
    deleteCard,
    getTags,
    createTag,
    updateTagStyle,
    deleteTag
  };
}

export const boardApi = createBoardApi();
