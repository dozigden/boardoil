import type { Board, Card, Column, Tag, TagStyleName } from '../types/boardTypes';
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
    return postData<Column>('/api/columns', { title });
  }

  async function saveColumn(
    columnId: number,
    title: string
  ): Promise<Result<Column, AppError>> {
    return patchData<Column>(`/api/columns/${columnId}`, { title });
  }

  async function moveColumn(columnId: number, positionAfterColumnId: number | null): Promise<Result<Column, AppError>> {
    return patchData<Column>(`/api/columns/${columnId}/move`, { positionAfterColumnId });
  }

  async function deleteColumn(columnId: number): Promise<Result<void, AppError>> {
    return deleteJson(`/api/columns/${columnId}`);
  }

  async function createCard(columnId: number, title: string): Promise<Result<Card, AppError>> {
    return postData<Card>('/api/cards', {
      boardColumnId: columnId,
      title,
      description: '',
      tagNames: []
    });
  }

  async function saveCard(
    cardId: number,
    title: string,
    description: string,
    tagNames: string[]
  ): Promise<Result<Card, AppError>> {
    return patchData<Card>(`/api/cards/${cardId}`, {
      title,
      description,
      tagNames
    });
  }

  async function moveCard(
    cardId: number,
    boardColumnId: number,
    positionAfterCardId: number | null
  ): Promise<Result<Card, AppError>> {
    return patchData<Card>(`/api/cards/${cardId}/move`, {
      boardColumnId,
      positionAfterCardId
    });
  }

  async function deleteCard(cardId: number): Promise<Result<void, AppError>> {
    return deleteJson(`/api/cards/${cardId}`);
  }

  async function getTags(): Promise<Result<Tag[], AppError>> {
    const envelopeResult = await getEnvelope<Tag[]>('/api/tags');
    if (!envelopeResult.ok) {
      return envelopeResult;
    }

    return ok(envelopeResult.data.data ?? []);
  }

  async function createTag(name: string): Promise<Result<Tag, AppError>> {
    return postData<Tag>('/api/tags', { name });
  }

  async function updateTagStyle(
    tagName: string,
    styleName: TagStyleName,
    stylePropertiesJson: string
  ): Promise<Result<Tag, AppError>> {
    const encodedTagName = encodeURIComponent(tagName);
    return patchData<Tag>(`/api/tags/${encodedTagName}`, {
      styleName,
      stylePropertiesJson
    });
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
    deleteCard,
    getTags,
    createTag,
    updateTagStyle
  };
}

export const boardApi = createBoardApi();
