import type { BoardApi } from '../shared/api/boardApi';
import { defaultBoardAttachmentInventoryQuery, type CardAttachment } from '../shared/types/attachmentTypes';
import type {
  ArchivedCard,
  Board,
  BoardMember,
  Card,
  CardComment,
  CardEditModel,
  CardTag,
  CardType,
  Slick,
  Tag
} from '../shared/types/boardTypes';
import type { AppError } from '../shared/types/appError';
import type { Result } from '../shared/types/result';
import { err, ok } from '../shared/types/result';
import catTaxImageUrl from './assets/pay-the-cat-tax.webp?url';
import catTaxThumbnailUrl from './assets/pay-the-cat-tax-thumbnail.webp?url';
import restyleButtonsImageUrl from './assets/restyle-buttons.webp?url';
import restyleButtonsThumbnailUrl from './assets/restyle-buttons-thumbnail.webp?url';
import webTrafficPieChartImageUrl from './assets/web-traffic-pie-chart.webp?url';
import webTrafficPieChartThumbnailUrl from './assets/web-traffic-pie-chart-thumbnail.webp?url';

const DemoBoardId = 1;
const DemoUserId = 1;
const SortKeyLength = 20;
const SortKeyAlphabet = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ';
const SortKeyBase = BigInt(SortKeyAlphabet.length);
const MaximumSortKeyValue = (SortKeyBase ** BigInt(SortKeyLength)) - 1n;
const SortKeySpaceSize = MaximumSortKeyValue + 1n;
const PreferredSortKeyRangeStart = SortKeySpaceSize / 4n;
const PreferredSortKeyRangeSize = SortKeySpaceSize / 2n;
const DemoAttachmentTimestamp = '2026-08-12T09:00:00.000Z';

type DemoAttachmentImage = {
  cardId: number;
  attachment: CardAttachment;
  imageUrl: string;
  thumbnailUrl: string;
};

const demoAttachmentImages: DemoAttachmentImage[] = [
  makeDemoAttachmentImage(1, 101, 'restyle-buttons.webp', 5468,
    restyleButtonsImageUrl, restyleButtonsThumbnailUrl),
  makeDemoAttachmentImage(2, 105, 'web-traffic-pie-chart.webp', 5164,
    webTrafficPieChartImageUrl, webTrafficPieChartThumbnailUrl),
  makeDemoAttachmentImage(3, 106, 'pay-the-cat-tax.webp', 11942,
    catTaxImageUrl, catTaxThumbnailUrl)
];

type DemoState = {
  version: 1;
  board: Board;
  tags: Tag[];
  slicks: Slick[];
  cardTypes: CardType[];
  members: BoardMember[];
  comments: Record<number, CardComment[]>;
  archivedCards: ArchivedCard[];
  nextCardId: number;
  nextTagId: number;
  nextSlickId: number;
  nextCommentId: number;
};

let state = createSeedState();

export function createDemoBoardApi(): BoardApi {
  return demoBoardApi;
}

export function resetDemoData() {
  state = createSeedState();
}

const demoBoardApi: BoardApi = {
  supportsAttachments: true,
  supportsAttachmentMutations: false,
  async getBoardAttachments(boardId, query = defaultBoardAttachmentInventoryQuery) {
    if (boardId !== DemoBoardId) { return notFound('Board not found.'); }
    const items = demoAttachmentImages.flatMap(image => {
      const live = findCard(image.cardId)?.card;
      const archived = state.archivedCards.find(card => card.id === image.cardId);
      const card = live ?? archived;
      if (!card) { return []; }
      return [{ ...clone(image.attachment), cardId: card.id, cardTitle: card.title, archived: !live }];
    });
    const matching = items.filter(item => {
      if (query.state === 'live') { return !item.archived; }
      if (query.state === 'archived') { return item.archived; }
      return true;
    }).sort((a, b) => {
      let comparison: number;
      if (query.sort === 'size') { comparison = a.byteLength - b.byteLength; }
      else if (query.sort === 'name') {
        const firstName = a.originalFileName.toUpperCase();
        const secondName = b.originalFileName.toUpperCase();
        comparison = 0;
        if (firstName < secondName) { comparison = -1; }
        else if (firstName > secondName) { comparison = 1; }
      } else { comparison = a.createdAtUtc.localeCompare(b.createdAtUtc); }
      const stableComparison = comparison || a.id - b.id;
      return query.direction === 'asc' ? stableComparison : -stableComparison;
    });
    return ok({
      items: matching.slice(query.offset, query.offset + query.limit),
      totalCount: items.length,
      totalByteLength: items.reduce((total, item) => total + item.byteLength, 0),
      matchingCount: matching.length, offset: query.offset, limit: query.limit
    });
  },
  async getAttachments(boardId, cardId, archived) {
    if (boardId !== DemoBoardId || !hasCard(cardId, archived)) {
      return notFound('Card not found.');
    }

    const items = demoAttachmentImages
      .filter(image => image.cardId === cardId)
      .map(image => clone(image.attachment));
    return ok({ items, maxUploadByteLength: 0 });
  },
  async getCardThumbnails(boardId, cardIds) {
    if (boardId !== DemoBoardId) {
      return notFound('Board not found.');
    }

    const requestedCardIds = cardIds ? new Set(cardIds) : null;
    return ok(demoAttachmentImages
      .filter(image => findCard(image.cardId) && (!requestedCardIds || requestedCardIds.has(image.cardId)))
      .map(image => ({
        cardId: image.cardId,
        attachmentId: image.attachment.id,
        originalFileName: image.attachment.originalFileName,
        hasThumbnail: image.attachment.hasThumbnail
      })));
  },
  async uploadAttachment() { return unavailable(); },
  async getAttachmentImage(boardId, cardId, archived, fileName) {
    if (boardId !== DemoBoardId || !hasCard(cardId, archived)) {
      return notFound('Card not found.');
    }

    const image = demoAttachmentImages.find(candidate =>
      candidate.cardId === cardId && candidate.attachment.originalFileName === fileName);
    return image ? loadDemoImage(image.imageUrl) : notFound('Image not found.');
  },
  async getAttachmentThumbnail(boardId, attachmentId) {
    if (boardId !== DemoBoardId) {
      return notFound('Board not found.');
    }

    const image = demoAttachmentImages.find(candidate => candidate.attachment.id === attachmentId);
    return image ? loadDemoImage(image.thumbnailUrl) : notFound('Thumbnail not found.');
  },
  async putAttachmentThumbnail() { return unavailable(); },
  async downloadAttachment(boardId, attachmentId) {
    if (boardId !== DemoBoardId) {
      return notFound('Board not found.');
    }

    const image = demoAttachmentImages.find(candidate => candidate.attachment.id === attachmentId);
    if (!image) {
      return notFound('Attachment not found.');
    }

    const blobResult = await loadDemoImage(image.imageUrl);
    return blobResult.ok
      ? ok({ fileName: image.attachment.originalFileName, contentType: image.attachment.contentType, blob: blobResult.data })
      : blobResult;
  },
  async deleteAttachment() { return unavailable(); },
  async deleteBoardAttachment() { return unavailable(); },
  async duplicateCard(boardId, _cardId, model) { return demoBoardApi.createCard(boardId, model); },
  async getBoards() {
    return ok([toBoardSummary(state.board)]);
  },

  async getBoard(boardId) {
    if (boardId !== DemoBoardId) {
      return notFound('Board not found.');
    }

    return ok(clone(state.board));
  },

  async createBoard() {
    return unavailable();
  },

  async cloneBoard() {
    return unavailable();
  },

  async importBoardPackage() {
    return unavailable();
  },

  async exportBoard() {
    return unavailable();
  },

  async saveBoard() {
    return unavailable();
  },

  async deleteBoard() {
    return unavailable();
  },

  async getBoardMembers(boardId) {
    if (boardId !== DemoBoardId) {
      return notFound('Board not found.');
    }

    return ok(clone(state.members));
  },

  async addBoardMember() {
    return unavailable();
  },

  async updateBoardMemberRole() {
    return unavailable();
  },

  async removeBoardMember() {
    return unavailable();
  },

  async getColumns(boardId) {
    if (boardId !== DemoBoardId) {
      return notFound('Board not found.');
    }

    return ok(state.board.columns.map(({ cards: _cards, ...column }) => clone(column)));
  },

  async createColumn() {
    return unavailable();
  },

  async saveColumn() {
    return unavailable();
  },

  async moveColumn() {
    return unavailable();
  },

  async deleteColumn() {
    return unavailable();
  },

  async createCard(boardId, model) {
    if (boardId !== DemoBoardId) {
      return notFound('Board not found.');
    }

    const column = getColumn(model.boardColumnId);
    const cardType = getCardType(model.cardTypeId);
    const title = model.title.trim();
    if (!column || !cardType || title.length === 0) {
      return validationError('Card title, column, and type are required.');
    }

    const timestamp = now();
    const card: Card = {
      id: state.nextCardId++,
      slick: null,
      boardColumnId: column.id,
      cardTypeId: cardType.id,
      slickId: null,
      slickName: null,
      cardTypeName: cardType.name,
      cardTypeEmoji: cardType.emoji,
      assignedUserId: null,
      assignedUserDisplayName: null,
      assignedUserImageRelativePath: null,
      title,
      description: '',
      externalUrl: null,
      sortKey: createLeadingCardSortKey(column),
      tags: [],
      tagNames: [],
      cardCreatedUtc: timestamp,
      cardUpdatedUtc: timestamp
    };
    applyCardEdit(card, model, cardType);
    card.cardCreatedUtc = timestamp;
    card.cardUpdatedUtc = timestamp;
    column.cards.unshift(card);
    return ok(clone(card));
  },

  async saveCard(boardId, cardId, model) {
    if (boardId !== DemoBoardId) {
      return notFound('Board not found.');
    }

    const located = findCard(cardId);
    const targetColumn = getColumn(model.boardColumnId);
    const cardType = getCardType(model.cardTypeId);
    const title = model.title.trim();
    if (!located || !targetColumn || !cardType || title.length === 0) {
      return validationError('Card title, column, and type are required.');
    }

    const card = located.card;
    if (located.column.id !== targetColumn.id) {
      located.column.cards.splice(located.index, 1);
      card.sortKey = createSortKeyBetween(targetColumn.cards[targetColumn.cards.length - 1]?.sortKey ?? null, null);
      targetColumn.cards.push(card);
      card.boardColumnId = targetColumn.id;
    }

    applyCardEdit(card, model, cardType);
    return ok(clone(card));
  },

  async moveCard(boardId, cardId, boardColumnId, positionAfterCardId) {
    if (boardId !== DemoBoardId) {
      return notFound('Board not found.');
    }

    const movedCard = moveCardInternal(cardId, boardColumnId, positionAfterCardId);
    if (!movedCard) {
      return validationError('The card could not be moved.');
    }

    return ok(clone(movedCard));
  },

  async transferCard() {
    return unavailable();
  },

  async editCards(boardId, request) {
    if (boardId !== DemoBoardId) {
      return notFound('Board not found.');
    }

    const editedCards: Card[] = [];
    const cardIds = [...new Set(request.cardIds)];
    if (request.move) {
      let positionAfterCardId = request.move.positionAfterCardId;
      for (const cardId of cardIds) {
        const moved = moveCardInternal(cardId, request.move.targetColumnId, positionAfterCardId);
        if (moved) {
          positionAfterCardId = moved.id;
        }
      }
    }

    for (const cardId of cardIds) {
      const located = findCard(cardId);
      if (!located) {
        continue;
      }

      const card = located.card;
      const nextTagNames = new Set(card.tagNames);
      for (const tagName of request.addTagNames ?? []) {
        nextTagNames.add(ensureTag(tagName).name);
      }
      for (const tagName of request.removeTagNames ?? []) {
        for (const existingName of nextTagNames) {
          if (existingName.localeCompare(tagName, undefined, { sensitivity: 'accent' }) === 0) {
            nextTagNames.delete(existingName);
          }
        }
      }

      setCardTags(card, [...nextTagNames]);
      if (request.slick !== undefined) {
        setCardSlick(card, request.slick?.name ?? null);
      }
      const updatedAtUtc = now();
      card.cardUpdatedUtc = updatedAtUtc;
      editedCards.push(clone(card));
    }

    return ok(editedCards);
  },

  async deleteCard(boardId, cardId) {
    if (boardId !== DemoBoardId) {
      return notFound('Board not found.');
    }

    deleteCardInternal(cardId);
    return ok(undefined);
  },

  async deleteCards(boardId, cardIds) {
    if (boardId !== DemoBoardId) {
      return notFound('Board not found.');
    }

    let deletedCount = 0;
    for (const cardId of new Set(cardIds)) {
      if (deleteCardInternal(cardId)) {
        deletedCount += 1;
      }
    }
    return ok({ boardId, requestedCount: cardIds.length, deletedCount });
  },

  async getCardComments(boardId, cardId) {
    if (boardId !== DemoBoardId) {
      return notFound('Board not found.');
    }

    return ok(clone(state.comments[cardId] ?? []));
  },

  async createCardComment(boardId, cardId, text) {
    if (boardId !== DemoBoardId || !findCard(cardId)) {
      return notFound('Card not found.');
    }

    const member = state.members.find(candidate => candidate.userId === DemoUserId)!;
    const postedAtUtc = now();
    const comment: CardComment = {
      id: state.nextCommentId++,
      cardId,
      authorUserId: DemoUserId,
      authorDisplayName: member.displayName,
      authorImageRelativePath: null,
      text: text.trim(),
      postedAtUtc
    };
    state.comments[cardId] = [comment, ...(state.comments[cardId] ?? [])];
    const card = findCard(cardId)!.card;
    card.cardUpdatedUtc = postedAtUtc;
    return ok(clone(comment));
  },

  async archiveCard(boardId, cardId) {
    if (boardId !== DemoBoardId) {
      return notFound('Board not found.');
    }

    archiveCardInternal(cardId);
    return ok(undefined);
  },

  async archiveCards(boardId, cardIds) {
    if (boardId !== DemoBoardId) {
      return notFound('Board not found.');
    }

    let archivedCount = 0;
    for (const cardId of new Set(cardIds)) {
      if (archiveCardInternal(cardId)) {
        archivedCount += 1;
      }
    }
    return ok({ boardId, requestedCount: cardIds.length, archivedCount });
  },

  async unarchiveCard(boardId, boardCardId) {
    if (boardId !== DemoBoardId) {
      return notFound('Board not found.');
    }

    const archiveIndex = state.archivedCards.findIndex(candidate => candidate.id === boardCardId);
    if (archiveIndex < 0) {
      return notFound('Archived card not found.');
    }

    const archivedCard = state.archivedCards[archiveIndex]!;
    const column = getColumn(archivedCard.card.boardColumnId) ?? state.board.columns[0]!;
    const restoredCard = clone(archivedCard.card);
    restoredCard.boardColumnId = column.id;
    restoredCard.sortKey = createSortKeyBetween(column.cards[column.cards.length - 1]?.sortKey ?? null, null);
    column.cards.push(restoredCard);
    state.archivedCards.splice(archiveIndex, 1);
    return ok(clone(restoredCard));
  },

  async getArchivedCards(boardId, options) {
    if (boardId !== DemoBoardId) {
      return notFound('Board not found.');
    }

    const searchText = options?.searchText?.trim().toLocaleLowerCase() ?? '';
    const offset = options?.offset ?? 0;
    const limit = options?.limit ?? 25;
    const matching = state.archivedCards.filter(card => {
      if (!searchText) {
        return true;
      }

      return `${card.title} ${card.tagNames.join(' ')}`.toLocaleLowerCase().includes(searchText);
    });
    return ok({
      items: clone(matching.slice(offset, offset + limit).map(({ card: _card, ...item }) => item)),
      offset,
      limit,
      totalCount: matching.length
    });
  },

  async getArchivedCard(boardId, boardCardId) {
    if (boardId !== DemoBoardId) {
      return notFound('Board not found.');
    }

    const archivedCard = state.archivedCards.find(candidate => candidate.id === boardCardId);
    return archivedCard ? ok(clone(archivedCard)) : notFound('Archived card not found.');
  },

  async getTags(boardId) {
    return boardId === DemoBoardId ? ok(clone(state.tags)) : notFound('Board not found.');
  },

  async getTagCreateDefaultStyle(boardId) {
    if (boardId !== DemoBoardId) {
      return notFound('Board not found.');
    }
    return ok({ styleName: 'presets', stylePropertiesJson: '{"presetIndex":1}' });
  },

  async createTag(boardId, name, emoji) {
    if (boardId !== DemoBoardId) {
      return notFound('Board not found.');
    }

    const tag = ensureTag(name, emoji);
    return ok(clone(tag));
  },

  async updateTagStyle(boardId, tagId, model) {
    if (boardId !== DemoBoardId) {
      return notFound('Board not found.');
    }

    const tag = state.tags.find(candidate => candidate.id === tagId);
    if (!tag) {
      return notFound('Tag not found.');
    }

    tag.name = model.name.trim();
    tag.emoji = model.emoji;
    tag.styleName = model.styleName;
    tag.stylePropertiesJson = model.stylePropertiesJson;
    tag.updatedAtUtc = now();
    return ok(clone(tag));
  },

  async deleteTag() {
    return unavailable();
  },

  async getCardTypes(boardId) {
    return boardId === DemoBoardId ? ok(clone(state.cardTypes)) : notFound('Board not found.');
  },

  async createCardType() {
    return unavailable();
  },

  async updateCardType() {
    return unavailable();
  },

  async setDefaultCardType() {
    return unavailable();
  },

  async deleteCardType() {
    return unavailable();
  },

  async getSlicks(boardId) {
    return boardId === DemoBoardId ? ok(clone(state.slicks)) : notFound('Board not found.');
  },

  async getSlickCreateDefaultStyle(boardId) {
    if (boardId !== DemoBoardId) {
      return notFound('Board not found.');
    }
    return ok({ styleName: 'presets', stylePropertiesJson: '{"presetIndex":0,"textColorMode":"auto"}' });
  },

  async createSlick() {
    return unavailable();
  },

  async updateSlick() {
    return unavailable();
  },

  async deleteSlick() {
    return unavailable();
  }
};

function applyCardEdit(card: Card, model: CardEditModel, cardType: CardType) {
  card.title = model.title.trim();
  card.description = model.description;
  card.externalUrl = model.externalUrl;
  card.cardTypeId = cardType.id;
  card.cardTypeName = cardType.name;
  card.cardTypeEmoji = cardType.emoji;
  card.assignedUserId = model.assignedUserId;
  const member = state.members.find(candidate => candidate.userId === model.assignedUserId);
  card.assignedUserDisplayName = member?.displayName ?? null;
  card.assignedUserImageRelativePath = null;
  setCardTags(card, model.tagNames);
  setCardSlick(card, model.slickName);
  const updatedAtUtc = now();
  card.cardUpdatedUtc = updatedAtUtc;
}

function setCardTags(card: Card, tagNames: string[]) {
  const tags = tagNames.map(tagName => ensureTag(tagName));
  card.tags = tags.map(toCardTag);
  card.tagNames = tags.map(tag => tag.name);
}

function setCardSlick(card: Card, slickName: string | null) {
  const canonicalName = slickName?.trim() ?? '';
  if (!canonicalName) {
    card.slickId = null;
    card.slickName = null;
    card.slick = null;
    return;
  }

  const slick = ensureSlick(canonicalName);
  card.slickId = slick.id;
  card.slickName = slick.name;
  card.slick = { ...slick };
}

function moveCardInternal(cardId: number, targetColumnId: number, positionAfterCardId: number | null) {
  const located = findCard(cardId);
  const targetColumn = getColumn(targetColumnId);
  if (!located || !targetColumn) {
    return null;
  }

  located.column.cards.splice(located.index, 1);
  let insertIndex = 0;
  if (positionAfterCardId !== null) {
    const anchorIndex = targetColumn.cards.findIndex(candidate => candidate.id === positionAfterCardId);
    insertIndex = anchorIndex < 0 ? targetColumn.cards.length : anchorIndex + 1;
  }

  const previousSortKey = targetColumn.cards[insertIndex - 1]?.sortKey ?? null;
  const nextSortKey = targetColumn.cards[insertIndex]?.sortKey ?? null;
  located.card.boardColumnId = targetColumn.id;
  located.card.sortKey = createSortKeyBetween(previousSortKey, nextSortKey);
  const updatedAtUtc = now();
  located.card.cardUpdatedUtc = updatedAtUtc;
  targetColumn.cards.splice(insertIndex, 0, located.card);
  return located.card;
}

function deleteCardInternal(cardId: number) {
  const located = findCard(cardId);
  if (!located) {
    return false;
  }

  located.column.cards.splice(located.index, 1);
  delete state.comments[cardId];
  return true;
}

function archiveCardInternal(cardId: number) {
  const located = findCard(cardId);
  if (!located) {
    return false;
  }

  located.column.cards.splice(located.index, 1);
  const card = clone(located.card);
  state.archivedCards.unshift({
    id: card.id,
    boardId: DemoBoardId,
    title: card.title,
    tagNames: [...card.tagNames],
    archivedAtUtc: now(),
    card,
    comments: clone(state.comments[card.id] ?? []).map(comment => ({
      text: comment.text,
      postedAtUtc: comment.postedAtUtc,
      authorUserId: comment.authorUserId,
      authorDisplayName: comment.authorDisplayName ?? null,
      authorImageRelativePath: comment.authorImageRelativePath ?? null
    }))
  });
  return true;
}

function findCard(cardId: number) {
  for (const column of state.board.columns) {
    const index = column.cards.findIndex(card => card.id === cardId);
    if (index >= 0) {
      return { card: column.cards[index]!, column, index };
    }
  }

  return null;
}

function getColumn(columnId: number) {
  return state.board.columns.find(column => column.id === columnId) ?? null;
}

function getCardType(cardTypeId: number | null) {
  if (cardTypeId !== null) {
    const selected = state.cardTypes.find(cardType => cardType.id === cardTypeId);
    if (selected) {
      return selected;
    }
  }

  return state.cardTypes.find(cardType => cardType.isSystem) ?? null;
}

function ensureTag(name: string, emoji: string | null = null) {
  const canonicalName = name.trim();
  const existing = state.tags.find(tag => tag.name.toLocaleLowerCase() === canonicalName.toLocaleLowerCase());
  if (existing) {
    return existing;
  }

  const timestamp = now();
  const tag: Tag = {
    id: state.nextTagId++,
    name: canonicalName,
    emoji,
    styleName: 'presets',
    stylePropertiesJson: '{"presetIndex":1}',
    createdAtUtc: timestamp,
    updatedAtUtc: timestamp
  };
  state.tags.push(tag);
  return tag;
}

function ensureSlick(name: string) {
  const canonicalName = name.trim();
  const existing = state.slicks.find(slick => slick.name.toLocaleLowerCase() === canonicalName.toLocaleLowerCase());
  if (existing) {
    return existing;
  }

  const timestamp = now();
  const slick: Slick = {
    id: state.nextSlickId++,
    name: canonicalName,
    styleName: 'presets',
    stylePropertiesJson: '{"presetIndex":0}',
    createdAtUtc: timestamp,
    updatedAtUtc: timestamp
  };
  state.slicks.push(slick);
  return slick;
}

function createLeadingCardSortKey(column: Board['columns'][number]) {
  const firstSortKey = column.cards[0]?.sortKey;
  return createSortKeyBetween(null, firstSortKey ?? null);
}

function createSortKeyBetween(previous: string | null, next: string | null) {
  const low = previous === null ? -1n : parseSortKey(previous);
  const high = next === null ? MaximumSortKeyValue + 1n : parseSortKey(next);
  if (high <= low + 1n) {
    throw new Error('Unable to allocate a demo card sort key between neighbours.');
  }

  return formatSortKey((low + high) / 2n);
}

function parseSortKey(sortKey: string) {
  if (sortKey.length !== SortKeyLength) {
    throw new Error(`Sort key must be exactly ${SortKeyLength} characters.`);
  }

  let value = 0n;
  for (const rawCharacter of sortKey) {
    const digit = SortKeyAlphabet.indexOf(rawCharacter.toUpperCase());
    if (digit < 0) {
      throw new Error('Sort key contains an invalid character.');
    }

    value = (value * SortKeyBase) + BigInt(digit);
  }

  return value;
}

function formatSortKey(value: bigint) {
  if (value < 0n || value > MaximumSortKeyValue) {
    throw new Error('Sort key value is outside the supported range.');
  }

  const characters = new Array<string>(SortKeyLength);
  let remainder = value;
  for (let index = SortKeyLength - 1; index >= 0; index -= 1) {
    const digit = Number(remainder % SortKeyBase);
    characters[index] = SortKeyAlphabet[digit]!;
    remainder /= SortKeyBase;
  }

  return characters.join('');
}

function createEvenlySpacedSortKeys(count: number) {
  const spacing = PreferredSortKeyRangeSize / (BigInt(count) + 1n);
  return Array.from(
    { length: count },
    (_, index) => formatSortKey(PreferredSortKeyRangeStart + (spacing * BigInt(index + 1)))
  );
}

function createSeedState(): DemoState {
  const timestamp = '2026-08-12T09:00:00.000Z';
  const tags: Tag[] = [
    makeTag(1, 'Feature', '🎬️', 'presets', '{"presetIndex":0}', timestamp),
    makeTag(2, 'UI', '✨️', 'presets', '{"presetIndex":2}', timestamp),
    makeTag(3, 'MCP', '🤖', 'presets', '{"presetIndex":4}', timestamp),
    makeTag(4, 'Testing', '🧪', 'presets', '{"presetIndex":6}', timestamp),
    makeTag(5, 'Release', '🚀', 'presets', '{"presetIndex":8}', timestamp)
  ];
  const cardTypes: CardType[] = [
    {
      id: 1,
      name: 'Story',
      emoji: null,
      isSystem: true,
      styleName: 'auto',
      stylePropertiesJson: '{}',
      createdAtUtc: timestamp,
      updatedAtUtc: timestamp
    },
    {
      id: 2,
      name: 'Bug',
      emoji: '🕷️',
      isSystem: false,
      styleName: 'presets',
      stylePropertiesJson: '{"presetIndex":4,"textColorMode":"auto"}',
      createdAtUtc: timestamp,
      updatedAtUtc: timestamp
    }
  ];
  const slicks: Slick[] = [
    {
      id: 1,
      name: 'Launch polish',
      styleName: 'presets',
      stylePropertiesJson: '{"presetIndex":2,"textColorMode":"auto"}',
      createdAtUtc: timestamp,
      updatedAtUtc: timestamp
    }
  ];
  const members: BoardMember[] = [
    {
      userId: DemoUserId,
      userName: 'jane.doe',
      displayName: 'Jane Doe',
      profileImageRelativePath: null,
      role: 'Owner',
      createdAtUtc: timestamp,
      updatedAtUtc: timestamp
    },
    {
      userId: 2,
      userName: 'a.n.other',
      displayName: 'A. N. Other',
      profileImageRelativePath: null,
      role: 'Contributor',
      createdAtUtc: timestamp,
      updatedAtUtc: timestamp
    }
  ];
  const createCard = (
    id: number,
    columnId: number,
    title: string,
    tagIds: number[],
    options: { description?: string; cardTypeId?: number; assignedUserId?: number | null; slickId?: number | null } = {}
  ): Card => {
    const cardType = cardTypes.find(candidate => candidate.id === (options.cardTypeId ?? 1))!;
    const selectedTags = tagIds.map(tagId => tags.find(tag => tag.id === tagId)!).filter(Boolean);
    const assignedMember = members.find(member => member.userId === options.assignedUserId);
    const slick = slicks.find(candidate => candidate.id === options.slickId);
    return {
      id,
      boardColumnId: columnId,
      cardTypeId: cardType.id,
      cardTypeName: cardType.name,
      cardTypeEmoji: cardType.emoji,
      slickId: slick?.id ?? null,
      slickName: slick?.name ?? null,
      slick: slick ? { ...slick } : null,
      assignedUserId: assignedMember?.userId ?? null,
      assignedUserDisplayName: assignedMember?.displayName ?? null,
      assignedUserImageRelativePath: null,
      title,
      description: options.description ?? `Explore this card to see BoardOil's markdown editor, tags, assignments, and workflow controls.`,
      externalUrl: null,
      sortKey: '',
      tags: selectedTags.map(toCardTag),
      tagNames: selectedTags.map(tag => tag.name),
      cardCreatedUtc: timestamp,
      cardUpdatedUtc: timestamp
    };
  };

  const board: Board = {
    id: DemoBoardId,
    name: 'Live Demo',
    description: 'A fictional launch board for the interactive BoardOil preview.',
    slickCohesionModeEnabled: true,
    cardAttachmentThumbnailsEnabled: true,
    currentUserRole: 'Owner',
    createdAtUtc: timestamp,
    updatedAtUtc: timestamp,
    columns: [
      {
        id: 1,
        title: 'Ideas',
        sortKey: '001000',
        createdAtUtc: timestamp,
        updatedAtUtc: timestamp,
        cards: [
          createCard(101, 1, 'Restyle buttons', [1, 2], {
            description: imageDescription(
              'Bring the primary, secondary, and destructive actions into one consistent visual system.',
              'Three vertically stacked application buttons in BoardOil colours',
              restyleButtonsImageUrl)
          }),
          createCard(102, 1, 'Interactive onboarding checklist', [1]),
          createCard(103, 1, 'AI-assisted acceptance criteria', [3])
        ]
      },
      {
        id: 2,
        title: 'Ready',
        sortKey: '002000',
        createdAtUtc: timestamp,
        updatedAtUtc: timestamp,
        cards: [
          createCard(104, 2, 'Keyboard shortcut guide', [2]),
          createCard(105, 2, 'Include pie chart of web traffic', [1, 5], {
            description: imageDescription(
              'Add a quick traffic breakdown to the analytics summary.',
              'Rough hand-drawn pie chart of web traffic',
              webTrafficPieChartImageUrl),
            slickId: 1
          })
        ]
      },
      {
        id: 3,
        title: 'In progress',
        sortKey: '003000',
        createdAtUtc: timestamp,
        updatedAtUtc: timestamp,
        cards: [
          createCard(107, 3, 'Polish card editing flow', [1, 2], { assignedUserId: DemoUserId, slickId: 1 }),
          createCard(108, 3, 'Realtime reconnect banner', [4], { assignedUserId: 2 }),
          createCard(109, 3, 'Fix compact drag handles', [2], { cardTypeId: 2 })
        ]
      },
      {
        id: 4,
        title: 'Done',
        sortKey: '004000',
        createdAtUtc: timestamp,
        updatedAtUtc: timestamp,
        cards: [
          createCard(106, 4, 'Pay the cat tax', [2], {
            description: imageDescription(
              'Every project update is improved by paying the cat tax.',
              'Black-and-white cat sitting on a chair',
              catTaxImageUrl),
            assignedUserId: 2,
            slickId: 1
          }),
          createCard(110, 4, 'Agree launch success measures', [5]),
          createCard(111, 4, 'Publish visual design tokens', [2, 5], { slickId: 1 }),
          createCard(112, 4, 'Add critical browser smoke tests', [4]),
          createCard(113, 4, 'Document installation options', [1]),
          createCard(114, 4, 'Review keyboard accessibility', [2, 4]),
          createCard(115, 4, 'Prepare launch screenshots', [2, 5]),
          createCard(116, 4, 'Validate archive workflow', [4]),
          createCard(117, 4, 'Tune mobile card spacing', [2]),
          createCard(118, 4, 'Publish release notes', [5]),
          createCard(119, 4, 'Run final launch checklist', [1, 5])
        ]
      }
    ]
  };
  const seedState: DemoState = {
    version: 1,
    board,
    tags,
    slicks,
    cardTypes,
    members,
    comments: {
      107: [
        {
          id: 1,
          cardId: 107,
          authorUserId: 2,
          authorDisplayName: 'A. N. Other',
          authorImageRelativePath: null,
          text: 'The new editor flow is ready for a final interaction pass.',
          postedAtUtc: '2026-08-12T10:15:00.000Z'
        }
      ]
    },
    archivedCards: [],
    nextCardId: 120,
    nextTagId: 6,
    nextSlickId: 2,
    nextCommentId: 2
  };

  for (const column of seedState.board.columns) {
    const sortKeys = createEvenlySpacedSortKeys(column.cards.length);
    column.cards.forEach((card, index) => {
      card.sortKey = sortKeys[index]!;
    });
  }

  return seedState;
}

function makeTag(
  id: number,
  name: string,
  emoji: string,
  styleName: Tag['styleName'],
  stylePropertiesJson: string,
  timestamp: string
): Tag {
  return { id, name, emoji, styleName, stylePropertiesJson, createdAtUtc: timestamp, updatedAtUtc: timestamp };
}

function makeDemoAttachmentImage(
  attachmentId: number,
  cardId: number,
  originalFileName: string,
  byteLength: number,
  imageUrl: string,
  thumbnailUrl: string
): DemoAttachmentImage {
  return {
    cardId,
    attachment: {
      id: attachmentId,
      originalFileName,
      contentType: 'image/webp',
      byteLength,
      createdAtUtc: DemoAttachmentTimestamp,
      createdByUserId: DemoUserId,
      hasThumbnail: true
    },
    imageUrl,
    thumbnailUrl
  };
}

function imageDescription(introduction: string, alt: string, imageUrl: string) {
  return `${introduction}\n\n![${alt}](${absoluteDemoAssetUrl(imageUrl)})`;
}

function absoluteDemoAssetUrl(assetUrl: string) {
  if (typeof location === 'undefined') {
    return new URL(assetUrl, 'https://demo.invalid/').toString();
  }

  return new URL(assetUrl, location.href).toString();
}

function hasCard(cardId: number, archived: boolean) {
  if (archived) {
    return state.archivedCards.some(candidate => candidate.id === cardId);
  }

  return findCard(cardId) !== null;
}

async function loadDemoImage(assetUrl: string): Promise<Result<Blob, AppError>> {
  try {
    const response = await fetch(assetUrl);
    if (!response.ok) {
      return err({ kind: 'http', message: 'The demo image could not be loaded.', statusCode: response.status });
    }

    return ok(await response.blob());
  } catch {
    return err({ kind: 'network', message: 'The demo image could not be loaded.' });
  }
}

function toCardTag(tag: Tag): CardTag {
  return {
    id: tag.id,
    name: tag.name,
    emoji: tag.emoji,
    styleName: tag.styleName,
    stylePropertiesJson: tag.stylePropertiesJson
  };
}

function toBoardSummary(board: Board) {
  const { columns: _columns, ...summary } = board;
  return clone(summary);
}

function now() {
  return new Date().toISOString();
}

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

function unavailable<T>(): Result<T, AppError> {
  return err({ kind: 'api', message: 'This action is not available in the interactive preview.' });
}

function notFound<T>(message: string): Result<T, AppError> {
  return err({ kind: 'api', message });
}

function validationError<T>(message: string): Result<T, AppError> {
  return err({ kind: 'api', message, validationErrors: { '': [message] } });
}
