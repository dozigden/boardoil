import type {
  ArchiveCardsSummary,
  ArchivedCard,
  ArchivedCardList,
  Board,
  BoardEditModel,
  BoardMemberEditModel,
  BoardMember,
  CardCreateModel,
  BoardSummary,
  Card,
  CardEditModel,
  CardComment,
  CardTypeEditModel,
  CardType,
  Column,
  ColumnCreateModel,
  ColumnEditModel,
  DeleteCardsSummary,
  Slick,
  SlickEditModel,
  StyleDefault,
  Tag,
  TagEditModel
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

  async function saveBoard(
    boardId: number,
    model: BoardEditModel
  ): Promise<Result<BoardSummary, AppError>> {
    return putData<BoardSummary>(`/api/boards/${boardId}`, model);
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

  async function addBoardMember(boardId: number, model: BoardMemberEditModel): Promise<Result<BoardMember, AppError>> {
    return postData<BoardMember>(`/api/boards/${boardId}/members`, { userId: model.userId, role: model.role });
  }

  async function updateBoardMemberRole(boardId: number, model: BoardMemberEditModel): Promise<Result<BoardMember, AppError>> {
    return patchData<BoardMember>(`/api/boards/${boardId}/members/${model.userId}`, { role: model.role });
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

  async function createColumn(boardId: number, model: ColumnCreateModel): Promise<Result<Column, AppError>> {
    return postData<Column>(`/api/boards/${boardId}/columns`, model);
  }

  async function saveColumn(
    boardId: number,
    columnId: number,
    model: ColumnEditModel
  ): Promise<Result<Column, AppError>> {
    return putData<Column>(`/api/boards/${boardId}/columns/${columnId}`, model);
  }

  async function moveColumn(boardId: number, columnId: number, positionAfterColumnId: number | null): Promise<Result<Column, AppError>> {
    return patchData<Column>(`/api/boards/${boardId}/columns/${columnId}/move`, { positionAfterColumnId });
  }

  async function deleteColumn(boardId: number, columnId: number): Promise<Result<void, AppError>> {
    return deleteJson(`/api/boards/${boardId}/columns/${columnId}`);
  }

  async function createCard(boardId: number, model: CardCreateModel): Promise<Result<Card, AppError>> {
    return postData<Card>(`/api/boards/${boardId}/cards`, {
      boardColumnId: model.boardColumnId,
      title: model.title,
      description: '',
      externalUrl: null,
      tagNames: [],
      cardTypeId: model.cardTypeId
    });
  }

  async function saveCard(
    boardId: number,
    cardId: number,
    model: CardEditModel
  ): Promise<Result<Card, AppError>> {
    return putData<Card>(`/api/boards/${boardId}/cards/${cardId}`, model);
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

  async function editCards(
    boardId: number,
    request: {
      cardIds: number[];
      move: { targetColumnId: number; positionAfterCardId: number | null } | null;
      addTagNames?: string[] | null;
      removeTagNames?: string[] | null;
      slick?: { name: string | null } | null;
    }
  ): Promise<Result<Card[], AppError>> {
    return patchData<Card[]>(`/api/boards/${boardId}/cards/edit`, request);
  }

  async function deleteCard(boardId: number, cardId: number): Promise<Result<void, AppError>> {
    return deleteJson(`/api/boards/${boardId}/cards/${cardId}`);
  }

  async function deleteCards(boardId: number, cardIds: number[]): Promise<Result<DeleteCardsSummary, AppError>> {
    return postData<DeleteCardsSummary>(`/api/boards/${boardId}/cards/delete`, { cardIds });
  }

  async function getCardComments(boardId: number, cardId: number): Promise<Result<CardComment[], AppError>> {
    const envelopeResult = await getEnvelope<CardComment[]>(`/api/boards/${boardId}/cards/${cardId}/comments`);
    if (!envelopeResult.ok) {
      return envelopeResult;
    }

    return ok(envelopeResult.data.data ?? []);
  }

  async function createCardComment(boardId: number, cardId: number, text: string): Promise<Result<CardComment, AppError>> {
    return postData<CardComment>(`/api/boards/${boardId}/cards/${cardId}/comments`, { text });
  }

  async function archiveCard(boardId: number, cardId: number): Promise<Result<void, AppError>> {
    return postJson(`/api/boards/${boardId}/cards/${cardId}/archive`, {});
  }

  async function archiveCards(boardId: number, cardIds: number[]): Promise<Result<ArchiveCardsSummary, AppError>> {
    return postData<ArchiveCardsSummary>(`/api/boards/${boardId}/cards/archive`, { cardIds });
  }

  async function unarchiveCard(boardId: number, boardCardId: number): Promise<Result<Card, AppError>> {
    return postData<Card>(`/api/boards/${boardId}/cards/archived/${boardCardId}/unarchive`, {});
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

  async function getArchivedCard(boardId: number, boardCardId: number): Promise<Result<ArchivedCard, AppError>> {
    const envelopeResult = await getEnvelope<ArchivedCard>(`/api/boards/${boardId}/cards/archived/${boardCardId}`);
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

  async function getTagCreateDefaultStyle(boardId: number): Promise<Result<StyleDefault, AppError>> {
    const envelopeResult = await getEnvelope<StyleDefault>(`/api/boards/${boardId}/tags/create-default-style`);
    if (!envelopeResult.ok) {
      return envelopeResult;
    }

    if (!envelopeResult.data.data) {
      return err({
        kind: 'api',
        message: envelopeResult.data.message ?? 'Failed to load tag default style.'
      });
    }

    return ok(envelopeResult.data.data);
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
    model: TagEditModel
  ): Promise<Result<Tag, AppError>> {
    return putData<Tag>(`/api/boards/${boardId}/tags/${tagId}`, model);
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
    model: CardTypeEditModel
  ): Promise<Result<CardType, AppError>> {
    return postData<CardType>(`/api/boards/${boardId}/card-types`, model);
  }

  async function updateCardType(
    boardId: number,
    cardTypeId: number,
    model: CardTypeEditModel
  ): Promise<Result<CardType, AppError>> {
    return putData<CardType>(`/api/boards/${boardId}/card-types/${cardTypeId}`, model);
  }

  async function setDefaultCardType(boardId: number, cardTypeId: number): Promise<Result<void, AppError>> {
    return patchData<void>(`/api/boards/${boardId}/card-types/${cardTypeId}/default`, {});
  }

  async function deleteCardType(boardId: number, cardTypeId: number): Promise<Result<void, AppError>> {
    return deleteJson(`/api/boards/${boardId}/card-types/${cardTypeId}`);
  }

  async function getSlicks(boardId: number): Promise<Result<Slick[], AppError>> {
    const envelopeResult = await getEnvelope<Slick[]>(`/api/boards/${boardId}/slicks`);
    if (!envelopeResult.ok) {
      return envelopeResult;
    }

    return ok(envelopeResult.data.data ?? []);
  }

  async function getSlickCreateDefaultStyle(boardId: number): Promise<Result<StyleDefault, AppError>> {
    const envelopeResult = await getEnvelope<StyleDefault>(`/api/boards/${boardId}/slicks/create-default-style`);
    if (!envelopeResult.ok) {
      return envelopeResult;
    }

    if (!envelopeResult.data.data) {
      return err({
        kind: 'api',
        message: envelopeResult.data.message ?? 'Failed to load slick default style.'
      });
    }

    return ok(envelopeResult.data.data);
  }

  async function createSlick(
    boardId: number,
    model: SlickEditModel
  ): Promise<Result<Slick, AppError>> {
    return postData<Slick>(`/api/boards/${boardId}/slicks`, model);
  }

  async function updateSlick(
    boardId: number,
    slickId: number,
    model: SlickEditModel
  ): Promise<Result<Slick, AppError>> {
    return putData<Slick>(`/api/boards/${boardId}/slicks/${slickId}`, model);
  }

  async function deleteSlick(boardId: number, slickId: number): Promise<Result<void, AppError>> {
    return deleteJson(`/api/boards/${boardId}/slicks/${slickId}`);
  }

  return {
    getBoards,
    getBoard,
    createBoard,
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
    editCards,
    deleteCard,
    deleteCards,
    getCardComments,
    createCardComment,
    archiveCard,
    archiveCards,
    unarchiveCard,
    getArchivedCards,
    getArchivedCard,
    getTags,
    getTagCreateDefaultStyle,
    createTag,
    updateTagStyle,
    deleteTag,
    getCardTypes,
    createCardType,
    updateCardType,
    setDefaultCardType,
    deleteCardType,
    getSlicks,
    getSlickCreateDefaultStyle,
    createSlick,
    updateSlick,
    deleteSlick
  };
}

export const boardApi = createBoardApi();
