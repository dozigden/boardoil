import type {
  ArchivedCard,
  ArchivedCardList,
  Board,
  BoardMember,
  BoardMemberRole,
  BoardSummary,
  Card,
  CardType,
  Column,
  Tag,
  TagStyleName
} from '../types/boardTypes';
import type { AppError } from '../types/appError';
import type { Result } from '../types/result';
import { err, ok } from '../types/result';
import { deleteJson, getBinary, getEnvelope, patchData, postData, postFormData, postJson, putData } from './http';

export type BoardApi = ReturnType<typeof createBoardApi>;
export type BoardExportPackage = {
  fileName: string;
  contentType: string | null;
  blob: Blob;
};

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

  async function createBoard(name: string, description?: string): Promise<Result<Board, AppError>> {
    return postData<Board>('/api/boards', { name, description });
  }

  async function importTasksMdBoard(url: string): Promise<Result<Board, AppError>> {
    return postData<Board>('/api/boards/import/tasksmd', { url });
  }

  async function importBoardPackage(file: File, name?: string): Promise<Result<Board, AppError>> {
    const formData = new FormData();
    formData.append('file', file);
    if (typeof name === 'string' && name.trim().length > 0) {
      formData.append('name', name.trim());
    }

    return postFormData<Board>('/api/boards/import', formData);
  }

  async function exportBoard(boardId: number): Promise<Result<BoardExportPackage, AppError>> {
    const result = await getBinary(`/api/boards/${boardId}/export`);
    if (!result.ok) {
      return result;
    }

    return ok({
      fileName: result.data.fileName,
      contentType: result.data.contentType,
      blob: result.data.blob
    });
  }

  async function saveBoard(boardId: number, name: string, description?: string): Promise<Result<BoardSummary, AppError>> {
    return putData<BoardSummary>(`/api/boards/${boardId}`, { name, description });
  }

  async function deleteBoard(boardId: number): Promise<Result<void, AppError>> {
    return deleteJson(`/api/boards/${boardId}`);
  }

  async function getBoardMembers(boardId: number): Promise<Result<BoardMember[], AppError>> {
    const envelopeResult = await getEnvelope<BoardMember[]>(`/api/boards/${boardId}/members`);
    if (!envelopeResult.ok) {
      return envelopeResult;
    }

    return ok(envelopeResult.data.data ?? []);
  }

  async function addBoardMember(boardId: number, userId: number, role: BoardMemberRole): Promise<Result<BoardMember, AppError>> {
    return postData<BoardMember>(`/api/boards/${boardId}/members`, { userId, role });
  }

  async function updateBoardMemberRole(
    boardId: number,
    userId: number,
    role: BoardMemberRole
  ): Promise<Result<BoardMember, AppError>> {
    return patchData<BoardMember>(`/api/boards/${boardId}/members/${userId}`, { role });
  }

  async function removeBoardMember(boardId: number, userId: number): Promise<Result<void, AppError>> {
    return deleteJson(`/api/boards/${boardId}/members/${userId}`);
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

  async function createCard(boardId: number, columnId: number, title: string, cardTypeId?: number | null): Promise<Result<Card, AppError>> {
    return postData<Card>(`/api/boards/${boardId}/cards`, {
      boardColumnId: columnId,
      title,
      description: '',
      tagNames: [],
      cardTypeId
    });
  }

  async function saveCard(
    boardId: number,
    cardId: number,
    title: string,
    description: string,
    tagNames: string[],
    cardTypeId: number,
    boardColumnId: number
  ): Promise<Result<Card, AppError>> {
    return putData<Card>(`/api/boards/${boardId}/cards/${cardId}`, {
      title,
      description,
      tagNames,
      cardTypeId,
      boardColumnId
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

  async function archiveCard(boardId: number, cardId: number): Promise<Result<void, AppError>> {
    return postJson(`/api/boards/${boardId}/cards/${cardId}/archive`, {});
  }

  async function getArchivedCards(
    boardId: number,
    options?: { searchText?: string; offset?: number; limit?: number }
  ): Promise<Result<ArchivedCardList, AppError>> {
    const searchParams = new URLSearchParams();
    const normalisedSearch = options?.searchText?.trim() ?? '';
    if (normalisedSearch.length > 0) {
      searchParams.set('search', normalisedSearch);
    }
    if (typeof options?.offset === 'number') {
      searchParams.set('offset', String(options.offset));
    }
    if (typeof options?.limit === 'number') {
      searchParams.set('limit', String(options.limit));
    }

    const query = searchParams.toString();
    const path = query.length > 0
      ? `/api/boards/${boardId}/cards/archived?${query}`
      : `/api/boards/${boardId}/cards/archived`;
    const envelopeResult = await getEnvelope<ArchivedCardList>(path);
    if (!envelopeResult.ok) {
      return envelopeResult;
    }

    if (!envelopeResult.data.data) {
      return err({
        kind: 'api',
        message: envelopeResult.data.message ?? 'Failed to load archived cards.'
      });
    }

    return ok(envelopeResult.data.data);
  }

  async function getArchivedCard(boardId: number, archivedCardId: number): Promise<Result<ArchivedCard, AppError>> {
    const envelopeResult = await getEnvelope<ArchivedCard>(`/api/boards/${boardId}/cards/archived/${archivedCardId}`);
    if (!envelopeResult.ok) {
      return envelopeResult;
    }

    if (!envelopeResult.data.data) {
      return err({
        kind: 'api',
        message: envelopeResult.data.message ?? 'Failed to load archived card.'
      });
    }

    return ok(envelopeResult.data.data);
  }

  async function getTags(boardId: number): Promise<Result<Tag[], AppError>> {
    const envelopeResult = await getEnvelope<Tag[]>(`/api/boards/${boardId}/tags`);
    if (!envelopeResult.ok) {
      return envelopeResult;
    }

    return ok(envelopeResult.data.data ?? []);
  }

  async function createTag(boardId: number, name: string, emoji?: string | null): Promise<Result<Tag, AppError>> {
    const payload: { name: string; emoji?: string | null } = { name };
    if (emoji !== undefined) {
      payload.emoji = emoji;
    }

    return postData<Tag>(`/api/boards/${boardId}/tags`, payload);
  }

  async function updateTagStyle(
    boardId: number,
    tagId: number,
    name: string,
    styleName: TagStyleName,
    stylePropertiesJson: string,
    emoji?: string | null
  ): Promise<Result<Tag, AppError>> {
    const payload: { name: string; styleName: TagStyleName; stylePropertiesJson: string; emoji?: string | null } = {
      name,
      styleName,
      stylePropertiesJson
    };
    if (emoji !== undefined) {
      payload.emoji = emoji;
    }

    return putData<Tag>(`/api/boards/${boardId}/tags/${tagId}`, payload);
  }

  async function deleteTag(boardId: number, tagId: number): Promise<Result<void, AppError>> {
    return deleteJson(`/api/boards/${boardId}/tags/${tagId}`);
  }

  async function getCardTypes(boardId: number): Promise<Result<CardType[], AppError>> {
    const envelopeResult = await getEnvelope<CardType[]>(`/api/boards/${boardId}/card-types`);
    if (!envelopeResult.ok) {
      return envelopeResult;
    }

    return ok(envelopeResult.data.data ?? []);
  }

  async function createCardType(
    boardId: number,
    name: string,
    emoji?: string | null,
    styleName?: TagStyleName,
    stylePropertiesJson?: string
  ): Promise<Result<CardType, AppError>> {
    const payload: {
      name: string;
      emoji?: string | null;
      styleName?: TagStyleName;
      stylePropertiesJson?: string;
    } = { name };
    if (emoji !== undefined) {
      payload.emoji = emoji;
    }
    if (styleName !== undefined) {
      payload.styleName = styleName;
    }
    if (stylePropertiesJson !== undefined) {
      payload.stylePropertiesJson = stylePropertiesJson;
    }

    return postData<CardType>(`/api/boards/${boardId}/card-types`, payload);
  }

  async function updateCardType(
    boardId: number,
    cardTypeId: number,
    name: string,
    emoji?: string | null,
    styleName?: TagStyleName,
    stylePropertiesJson?: string
  ): Promise<Result<CardType, AppError>> {
    const payload: {
      name: string;
      emoji?: string | null;
      styleName?: TagStyleName;
      stylePropertiesJson?: string;
    } = { name };
    if (emoji !== undefined) {
      payload.emoji = emoji;
    }
    if (styleName !== undefined) {
      payload.styleName = styleName;
    }
    if (stylePropertiesJson !== undefined) {
      payload.stylePropertiesJson = stylePropertiesJson;
    }

    return putData<CardType>(`/api/boards/${boardId}/card-types/${cardTypeId}`, payload);
  }

  async function setDefaultCardType(boardId: number, cardTypeId: number): Promise<Result<void, AppError>> {
    return patchData<void>(`/api/boards/${boardId}/card-types/${cardTypeId}/default`, {});
  }

  async function deleteCardType(boardId: number, cardTypeId: number): Promise<Result<void, AppError>> {
    return deleteJson(`/api/boards/${boardId}/card-types/${cardTypeId}`);
  }

  return {
    getBoards,
    getBoard,
    createBoard,
    importTasksMdBoard,
    importBoardPackage,
    exportBoard,
    saveBoard,
    deleteBoard,
    getBoardMembers,
    addBoardMember,
    updateBoardMemberRole,
    removeBoardMember,
    getColumns,
    createColumn,
    saveColumn,
    moveColumn,
    deleteColumn,
    createCard,
    saveCard,
    moveCard,
    deleteCard,
    archiveCard,
    getArchivedCards,
    getArchivedCard,
    getTags,
    createTag,
    updateTagStyle,
    deleteTag,
    getCardTypes,
    createCardType,
    updateCardType,
    setDefaultCardType,
    deleteCardType
  };
}

export const boardApi = createBoardApi();
