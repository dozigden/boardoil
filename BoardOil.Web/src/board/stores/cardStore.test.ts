import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useCardStore } from './cardStore';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import type { AppError } from '../../shared/types/appError';
import type { Board, Card, CardEditModel } from '../../shared/types/boardTypes';
import { err, ok } from '../../shared/types/result';
import type { Result } from '../../shared/types/result';

const api = {
  createCard: vi.fn(),
  saveCard: vi.fn(),
  moveCard: vi.fn(),
  transferCard: vi.fn(),
  editCards: vi.fn(),
  deleteCard: vi.fn(),
  deleteCards: vi.fn(),
  archiveCard: vi.fn(),
  archiveCards: vi.fn()
};

vi.mock('../../shared/api/boardApi', () => ({
  createBoardApi: () => api
}));

describe('cardStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

  it('hydrates cards by column from a board snapshot', () => {
    const store = useCardStore();
    const board = makeBoard();

    store.replaceBoardCards(board.id, board.columns);

    expect(store.getCardById(101)?.title).toBe('Task A');
    expect(store.getCardById(999)).toBeNull();
    expect(store.getCardById(null)).toBeNull();
    expect(store.getCardsForColumn(1).map(x => x.id)).toEqual([101]);
    expect(store.getCardsForColumn(2)).toEqual([]);
  });

  it('creates a card incrementally without reloading board', async () => {
    const store = useCardStore();
    store.replaceBoardCards(1, makeBoard().columns);

    const created: Card = {
      id: 102,
      boardColumnId: 1,
      cardTypeId: 1,
      cardTypeName: 'Story',
      cardTypeEmoji: null,
      title: 'Task B',
      description: '',
      externalUrl: null,
      sortKey: '00000000000000000000',
      tags: [],
      tagNames: [],
      cardCreatedUtc: '2026-03-15T00:00:00Z',
      cardUpdatedUtc: '2026-03-15T00:00:00Z'
    };
    api.createCard.mockResolvedValue(ok(created));

    const model = makeCardEditModel({ title: 'Task B', cardTypeId: null });
    await store.createCard(model);

    expect(api.createCard).toHaveBeenCalledWith(1, model);
    expect(store.getCardsForColumn(1).map(x => x.id)).toEqual([102, 101]);
  });

  it('creates a card with an explicit card type id', async () => {
    const store = useCardStore();
    store.replaceBoardCards(1, makeBoard().columns);

    const created: Card = {
      id: 103,
      boardColumnId: 1,
      cardTypeId: 2,
      cardTypeName: 'Bug',
      cardTypeEmoji: '🕷️',
      title: 'Task C',
      description: '',
      externalUrl: null,
      sortKey: '00000000000000000000',
      tags: [],
      tagNames: [],
      cardCreatedUtc: '2026-03-15T00:00:00Z',
      cardUpdatedUtc: '2026-03-15T00:00:00Z'
    };
    api.createCard.mockResolvedValue(ok(created));

    const model = makeCardEditModel({ title: 'Task C', cardTypeId: 2 });
    await store.createCard(model);

    expect(api.createCard).toHaveBeenCalledWith(1, model);
    expect(store.getCardsForColumn(1).map(x => x.id)).toEqual([103, 101]);
  });

  it('moves card across columns incrementally', async () => {
    const store = useCardStore();
    store.replaceBoardCards(1, makeBoard().columns);

    const moved: Card = {
      id: 101,
      boardColumnId: 2,
      cardTypeId: 1,
      cardTypeName: 'Story',
      cardTypeEmoji: null,
      title: 'Task A',
      description: 'Seed',
      externalUrl: null,
      sortKey: '00000000000000000001',
      tags: [],
      tagNames: [],
      cardCreatedUtc: '2026-03-15T00:00:00Z',
      cardUpdatedUtc: '2026-03-15T00:01:00Z'
    };
    api.moveCard.mockResolvedValue(ok(moved));

    store.startDrag(101, 1);
    await store.dropCard(2, null);

    expect(store.getCardsForColumn(1)).toHaveLength(0);
    expect(store.getCardsForColumn(2).map(x => x.id)).toEqual([101]);
    expect(api.moveCard).toHaveBeenCalledWith(1, 101, 2, null);
  });

  it('transfers a card and removes it from the source board state', async () => {
    const store = useCardStore();
    store.replaceBoardCards(1, makeBoard().columns);
    const transferredCard = {
      ...store.getCardById(101)!,
      id: 7,
      boardColumnId: 9
    };
    api.transferCard.mockResolvedValue(ok({ boardId: 2, card: transferredCard }));

    const result = await store.transferCard(101, 2, 9, 'keepMatching');

    expect(result).toEqual(ok({ boardId: 2, card: transferredCard }));
    expect(api.transferCard).toHaveBeenCalledWith(1, 101, 2, 9, 'keepMatching');
    expect(store.getCardById(101)).toBeNull();
  });

  it('moves multiple cards with a single bulk edit call', async () => {
    const store = useCardStore();
    const board = makeBoard();
    board.columns[0].cards.push({
      id: 102,
      boardColumnId: 1,
      cardTypeId: 1,
      cardTypeName: 'Story',
      cardTypeEmoji: null,
      title: 'Task B',
      description: '',
      externalUrl: null,
      sortKey: '00000000000000000002',
      tags: [],
      tagNames: [],
      cardCreatedUtc: '2026-03-15T00:00:00Z',
      cardUpdatedUtc: '2026-03-15T00:00:00Z'
    });
    board.columns[1].cards.push({
      id: 201,
      boardColumnId: 2,
      cardTypeId: 1,
      cardTypeName: 'Story',
      cardTypeEmoji: null,
      title: 'Task C',
      description: '',
      externalUrl: null,
      sortKey: '00000000000000000003',
      tags: [],
      tagNames: [],
      cardCreatedUtc: '2026-03-15T00:00:00Z',
      cardUpdatedUtc: '2026-03-15T00:00:00Z'
    });
    store.replaceBoardCards(board.id, board.columns);

    api.editCards.mockResolvedValue(ok([
      { ...store.getCardById(101)!, boardColumnId: 2, sortKey: '00000000000000000004' },
      { ...store.getCardById(102)!, boardColumnId: 2, sortKey: '00000000000000000005' }
    ]));

    const moved = await store.bulkMoveCards([101, 102], 2, null);

    expect(moved).toBe(true);
    expect(api.editCards).toHaveBeenCalledWith(1, {
      cardIds: [101, 102],
      move: { targetColumnId: 2, positionAfterCardId: 201 },
      addTagNames: [],
      removeTagNames: [],
      slick: undefined
    });
    expect(store.getCardsForColumn(1)).toHaveLength(0);
    expect(store.getCardsForColumn(2).map(x => x.id)).toEqual([201, 101, 102]);
  });

  it('bulk edits selected cards with tag operations and no move', async () => {
    const store = useCardStore();
    store.replaceBoardCards(1, makeBoard().columns);
    api.editCards.mockResolvedValue(ok([
      {
        ...store.getCardById(101)!,
        tagNames: ['Feature'],
        tags: []
      }
    ]));

    const edited = await store.bulkEditCards([101], {
      addTagNames: ['Feature'],
      removeTagNames: ['Old']
    });

    expect(edited).toBe(true);
    expect(api.editCards).toHaveBeenCalledWith(1, {
      cardIds: [101],
      move: null,
      addTagNames: ['Feature'],
      removeTagNames: ['Old'],
      slick: undefined
    });
    expect(store.getCardById(101)?.tagNames).toEqual(['Feature']);
  });

  it('bulk edits selected cards with slick clear operation', async () => {
    const store = useCardStore();
    store.replaceBoardCards(1, makeBoard().columns);
    api.editCards.mockResolvedValue(ok([
      {
        ...store.getCardById(101)!,
        slickId: null,
        slickName: null
      }
    ]));

    const edited = await store.bulkEditCards([101], {
      slickName: null
    });

    expect(edited).toBe(true);
    expect(api.editCards).toHaveBeenCalledWith(1, {
      cardIds: [101],
      move: null,
      addTagNames: [],
      removeTagNames: [],
      slick: { name: null }
    });
    expect(store.getCardById(101)?.slickId).toBeNull();
  });

  it('bulk edits selected cards with slick set operation', async () => {
    const store = useCardStore();
    store.replaceBoardCards(1, makeBoard().columns);
    api.editCards.mockResolvedValue(ok([
      {
        ...store.getCardById(101)!,
        slickId: 8,
        slickName: 'Release train'
      }
    ]));

    const edited = await store.bulkEditCards([101], {
      slickName: '  Release train  '
    });

    expect(edited).toBe(true);
    expect(api.editCards).toHaveBeenCalledWith(1, {
      cardIds: [101],
      move: null,
      addTagNames: [],
      removeTagNames: [],
      slick: { name: 'Release train' }
    });
    expect(store.getCardById(101)?.slickName).toBe('Release train');
  });

  it('translates drop-before-card into predecessor anchor', async () => {
    const store = useCardStore();
    const board = makeBoard();
    board.columns[1].cards = [
      {
        id: 201,
        boardColumnId: 2,
        cardTypeId: 1,
        cardTypeName: 'Story',
        cardTypeEmoji: null,
        title: 'Task B',
        description: 'Seed',
        externalUrl: null,
        sortKey: '00000000000000000010',
        tags: [],
        tagNames: [],
        cardCreatedUtc: '2026-03-15T00:00:00Z',
        cardUpdatedUtc: '2026-03-15T00:00:00Z'
      },
      {
        id: 202,
        boardColumnId: 2,
        cardTypeId: 1,
        cardTypeName: 'Story',
        cardTypeEmoji: null,
        title: 'Task C',
        description: 'Seed',
        externalUrl: null,
        sortKey: '00000000000000000020',
        tags: [],
        tagNames: [],
        cardCreatedUtc: '2026-03-15T00:00:00Z',
        cardUpdatedUtc: '2026-03-15T00:00:00Z'
      }
    ];
    store.replaceBoardCards(board.id, board.columns);

    const moved: Card = {
      id: 101,
      boardColumnId: 2,
      cardTypeId: 1,
      cardTypeName: 'Story',
      cardTypeEmoji: null,
      title: 'Task A',
      description: 'Seed',
      externalUrl: null,
      sortKey: '00000000000000000015',
      tags: [],
      tagNames: [],
      cardCreatedUtc: '2026-03-15T00:00:00Z',
      cardUpdatedUtc: '2026-03-15T00:01:00Z'
    };
    api.moveCard.mockResolvedValue(ok(moved));

    store.startDrag(101, 1);
    await store.dropCard(2, 202);

    expect(api.moveCard).toHaveBeenCalledWith(1, 101, 2, 201);
  });

  it('uses null anchor when dropping before first card', async () => {
    const store = useCardStore();
    const board = makeBoard();
    board.columns[1].cards = [
      {
        id: 201,
        boardColumnId: 2,
        cardTypeId: 1,
        cardTypeName: 'Story',
        cardTypeEmoji: null,
        title: 'Task B',
        description: 'Seed',
        externalUrl: null,
        sortKey: '00000000000000000010',
        tags: [],
        tagNames: [],
        cardCreatedUtc: '2026-03-15T00:00:00Z',
        cardUpdatedUtc: '2026-03-15T00:00:00Z'
      }
    ];
    store.replaceBoardCards(board.id, board.columns);

    const moved: Card = {
      id: 101,
      boardColumnId: 2,
      cardTypeId: 1,
      cardTypeName: 'Story',
      cardTypeEmoji: null,
      title: 'Task A',
      description: 'Seed',
      externalUrl: null,
      sortKey: '00000000000000000005',
      tags: [],
      tagNames: [],
      cardCreatedUtc: '2026-03-15T00:00:00Z',
      cardUpdatedUtc: '2026-03-15T00:01:00Z'
    };
    api.moveCard.mockResolvedValue(ok(moved));

    store.startDrag(101, 1);
    await store.dropCard(2, 201);

    expect(api.moveCard).toHaveBeenCalledWith(1, 101, 2, null);
  });

  it('saveCard updates card', async () => {
    const store = useCardStore();
    store.replaceBoardCards(1, makeBoard().columns);

    const updated: Card = {
      id: 101,
      boardColumnId: 1,
      cardTypeId: 1,
      cardTypeName: 'Story',
      cardTypeEmoji: null,
      title: 'Task A+',
      description: 'Updated',
      externalUrl: 'https://github.com/example/repository',
      sortKey: '00000000000000000001',
      tags: [
        {
          id: 7,
          name: 'Bug',
          styleName: 'solid',
          stylePropertiesJson: '{"backgroundColor":"#224466","textColorMode":"auto"}',
          emoji: null
        }
      ],
      tagNames: ['Bug'],
      cardCreatedUtc: '2026-03-15T00:00:00Z',
      cardUpdatedUtc: '2026-03-15T00:02:00Z'
    };
    api.saveCard.mockResolvedValue(ok(updated));

    const saved = await store.saveCard(101, {
      title: 'Task A+',
      description: 'Updated',
      externalUrl: 'https://github.com/example/repository',
      tagNames: ['Bug'],
      cardTypeId: 1,
      boardColumnId: 1,
      assignedUserId: null,
      slickName: null
    });

    expect(saved).toBe(true);
    expect(store.getCardById(101)?.title).toBe('Task A+');
    expect(store.getCardById(101)?.tagNames).toEqual(['Bug']);
    expect(api.saveCard).toHaveBeenCalledWith(1, 101, {
      title: 'Task A+',
      description: 'Updated',
      externalUrl: 'https://github.com/example/repository',
      tagNames: ['Bug'],
      cardTypeId: 1,
      boardColumnId: 1,
      assignedUserId: null,
      slickName: null
    });
  });

  it('saveCard returns false and keeps existing card when API save fails', async () => {
    const store = useCardStore();
    store.replaceBoardCards(1, makeBoard().columns);
    api.saveCard.mockResolvedValue(err({ kind: 'http', message: 'Unauthorized', statusCode: 401 }));

    const saved = await store.saveCard(101, {
      title: 'Task A+',
      description: 'Updated',
      externalUrl: null,
      tagNames: ['Bug'],
      cardTypeId: 1,
      boardColumnId: 1,
      assignedUserId: null,
      slickName: null
    });

    expect(saved).toBe(false);
    expect(store.getCardById(101)?.title).toBe('Task A');
  });

  it('does not apply an in-flight save response after switching to a board with the same card id', async () => {
    const store = useCardStore();
    const delayedSave = deferred<Result<Card, AppError>>();
    store.replaceBoardCards(1, makeBoard(1, 'Board one').columns);
    api.saveCard.mockImplementationOnce(() => delayedSave.promise);

    const pendingSave = store.saveCard(101, {
      title: 'Old board update',
      description: 'Updated',
      externalUrl: null,
      tagNames: [],
      cardTypeId: 1,
      boardColumnId: 1,
      assignedUserId: null,
      slickName: null
    });
    const boardTwo = makeBoard(2, 'Board two');
    boardTwo.columns[0].cards[0].title = 'Board two card';
    store.replaceBoardCards(2, boardTwo.columns);
    delayedSave.resolve(ok({
      ...boardTwo.columns[0].cards[0],
      title: 'Old board update'
    }));

    expect(await pendingSave).toBe(false);
    expect(api.saveCard).toHaveBeenCalledWith(1, 101, expect.any(Object));
    expect(store.getCardById(101)?.title).toBe('Board two card');
    expect(store.activeBoardId).toBe(2);
  });

  it('archiveCard removes card from active board cache', async () => {
    const store = useCardStore();
    store.replaceBoardCards(1, makeBoard().columns);
    api.archiveCard.mockResolvedValue(ok(undefined));

    const archived = await store.archiveCard(101);

    expect(archived).toBe(true);
    expect(api.archiveCard).toHaveBeenCalledWith(1, 101);
    expect(store.getCardById(101)).toBeNull();
    expect(store.getCardsForColumn(1)).toHaveLength(0);
  });

  it('archiveCards removes all archived cards from active board cache', async () => {
    const store = useCardStore();
    const board = makeBoard();
    board.columns[0].cards.push({
      id: 102,
      boardColumnId: 1,
      cardTypeId: 1,
      cardTypeName: 'Story',
      cardTypeEmoji: null,
      title: 'Task B',
      description: 'Seed',
      externalUrl: null,
      sortKey: '00000000000000000002',
      tags: [],
      tagNames: [],
      cardCreatedUtc: '2026-03-15T00:00:00Z',
      cardUpdatedUtc: '2026-03-15T00:00:00Z'
    });
    store.replaceBoardCards(1, board.columns);
    api.archiveCards.mockResolvedValue(ok({
      boardId: 1,
      requestedCount: 2,
      archivedCount: 2
    }));

    const archived = await store.archiveCards([101, 102, 102]);

    expect(archived).toBe(true);
    expect(api.archiveCards).toHaveBeenCalledWith(1, [101, 102]);
    expect(store.getCardById(101)).toBeNull();
    expect(store.getCardById(102)).toBeNull();
    expect(store.getCardsForColumn(1)).toHaveLength(0);
  });

  it('deleteCards removes all deleted cards from active board cache', async () => {
    const store = useCardStore();
    const board = makeBoard();
    board.columns[0].cards.push({
      id: 102,
      boardColumnId: 1,
      cardTypeId: 1,
      cardTypeName: 'Story',
      cardTypeEmoji: null,
      title: 'Task B',
      description: 'Seed',
      externalUrl: null,
      sortKey: '00000000000000000002',
      tags: [],
      tagNames: [],
      cardCreatedUtc: '2026-03-15T00:00:00Z',
      cardUpdatedUtc: '2026-03-15T00:00:00Z'
    });
    store.replaceBoardCards(1, board.columns);
    api.deleteCards.mockResolvedValue(ok({
      boardId: 1,
      requestedCount: 2,
      deletedCount: 2
    }));

    const deleted = await store.deleteCards([101, 102, 102]);

    expect(deleted).toBe(true);
    expect(api.deleteCards).toHaveBeenCalledWith(1, [101, 102]);
    expect(store.getCardById(101)).toBeNull();
    expect(store.getCardById(102)).toBeNull();
    expect(store.getCardsForColumn(1)).toHaveLength(0);
  });

  it('removeTagFromCards strips matching tags case-insensitively', () => {
    const store = useCardStore();
    const board = makeBoard();
    board.columns[0].cards[0].tagNames = ['Bug', 'urgent'];
    board.columns[0].cards[0].tags = [
      {
        id: 7,
        name: 'Bug',
        styleName: 'solid',
        stylePropertiesJson: '{"backgroundColor":"#224466","textColorMode":"auto"}',
        emoji: null
      },
      {
        id: 8,
        name: 'urgent',
        styleName: 'solid',
        stylePropertiesJson: '{"backgroundColor":"#113355","textColorMode":"auto"}',
        emoji: null
      }
    ];
    store.replaceBoardCards(board.id, board.columns);

    store.removeTagFromCards(' bug ');

    expect(store.getCardById(101)?.tagNames).toEqual(['urgent']);
    expect(store.getCardById(101)?.tags.map(tag => tag.name)).toEqual(['urgent']);
  });

  it('removeSlickFromCards clears slick membership from matching cards', () => {
    const store = useCardStore();
    const board = makeBoard();
    board.columns[0].cards[0].slickId = 15;
    store.replaceBoardCards(board.id, board.columns);

    store.removeSlickFromCards(15);

    expect(store.getCardById(101)?.slickId).toBeNull();
  });

  it('sets feedback error when API returns failure', async () => {
    const store = useCardStore();
    const feedback = useUiFeedbackStore();
    store.replaceBoardCards(1, makeBoard().columns);

    const apiError: AppError = {
      kind: 'api',
      message: 'Card create failed.'
    };
    api.createCard.mockResolvedValue(err(apiError));

    await store.createCard(makeCardEditModel({ title: 'Bad', cardTypeId: null }));

    expect(feedback.errorMessage).toBe('Card create failed.');
  });
});

function makeCardEditModel(overrides: Partial<CardEditModel> = {}): CardEditModel {
  return {
    boardColumnId: 1,
    title: 'Card',
    description: '',
    externalUrl: null,
    tagNames: [],
    cardTypeId: 1,
    assignedUserId: null,
    slickName: null,
    ...overrides
  };
}

function makeBoard(id = 1, name = 'Board'): Board {
  return {
    id,
    name,
    description: '',
    slickCohesionModeEnabled: true,
    createdAtUtc: '2026-03-15T00:00:00Z',
    updatedAtUtc: '2026-03-15T00:00:00Z',
    columns: [
      {
        id: 1,
        title: 'Backlog',
        sortKey: '00000000000000000010',
        createdAtUtc: '2026-03-15T00:00:00Z',
        updatedAtUtc: '2026-03-15T00:00:00Z',
        cards: [
          {
            id: 101,
            boardColumnId: 1,
            cardTypeId: 1,
            cardTypeName: 'Story',
            cardTypeEmoji: null,
            title: 'Task A',
            description: 'Seed',
            externalUrl: null,
            sortKey: '00000000000000000001',
            tags: [],
            tagNames: [],
            cardCreatedUtc: '2026-03-15T00:00:00Z',
            cardUpdatedUtc: '2026-03-15T00:00:00Z'
          }
        ]
      },
      {
        id: 2,
        title: 'Doing',
        sortKey: '00000000000000000020',
        createdAtUtc: '2026-03-15T00:00:00Z',
        updatedAtUtc: '2026-03-15T00:00:00Z',
        cards: []
      }
    ]
  };
}

function deferred<T>() {
  let resolve!: (value: T | PromiseLike<T>) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((promiseResolve, promiseReject) => {
    resolve = promiseResolve;
    reject = promiseReject;
  });

  return { promise, resolve, reject };
}
