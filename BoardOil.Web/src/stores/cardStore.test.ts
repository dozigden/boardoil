import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useCardStore } from './cardStore';
import { useUiFeedbackStore } from './uiFeedbackStore';
import type { AppError } from '../types/appError';
import type { Board, Card } from '../types/boardTypes';
import { err, ok } from '../types/result';

const api = {
  createCard: vi.fn(),
  saveCard: vi.fn(),
  moveCard: vi.fn(),
  deleteCard: vi.fn()
};

vi.mock('../api/boardApi', () => ({
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
      sortKey: '00000000000000000000',
      tags: [],
      tagNames: [],
      createdAtUtc: '2026-03-15T00:00:00Z',
      updatedAtUtc: '2026-03-15T00:00:00Z'
    };
    api.createCard.mockResolvedValue(ok(created));

    await store.createCard(1, 'Task B');

    expect(api.createCard).toHaveBeenCalledWith(1, 1, 'Task B');
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
      sortKey: '00000000000000000000',
      tags: [],
      tagNames: [],
      createdAtUtc: '2026-03-15T00:00:00Z',
      updatedAtUtc: '2026-03-15T00:00:00Z'
    };
    api.createCard.mockResolvedValue(ok(created));

    await store.createCard(1, 'Task C', 2);

    expect(api.createCard).toHaveBeenCalledWith(1, 1, 'Task C', 2);
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
      sortKey: '00000000000000000001',
      tags: [],
      tagNames: [],
      createdAtUtc: '2026-03-15T00:00:00Z',
      updatedAtUtc: '2026-03-15T00:01:00Z'
    };
    api.moveCard.mockResolvedValue(ok(moved));

    store.startDrag(101, 1);
    await store.dropCard(2, null);

    expect(store.getCardsForColumn(1)).toHaveLength(0);
    expect(store.getCardsForColumn(2).map(x => x.id)).toEqual([101]);
    expect(api.moveCard).toHaveBeenCalledWith(1, 101, 2, null);
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
        sortKey: '00000000000000000010',
        tags: [],
        tagNames: [],
        createdAtUtc: '2026-03-15T00:00:00Z',
        updatedAtUtc: '2026-03-15T00:00:00Z'
      },
      {
        id: 202,
        boardColumnId: 2,
        cardTypeId: 1,
        cardTypeName: 'Story',
        cardTypeEmoji: null,
        title: 'Task C',
        description: 'Seed',
        sortKey: '00000000000000000020',
        tags: [],
        tagNames: [],
        createdAtUtc: '2026-03-15T00:00:00Z',
        updatedAtUtc: '2026-03-15T00:00:00Z'
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
      sortKey: '00000000000000000015',
      tags: [],
      tagNames: [],
      createdAtUtc: '2026-03-15T00:00:00Z',
      updatedAtUtc: '2026-03-15T00:01:00Z'
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
        sortKey: '00000000000000000010',
        tags: [],
        tagNames: [],
        createdAtUtc: '2026-03-15T00:00:00Z',
        updatedAtUtc: '2026-03-15T00:00:00Z'
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
      sortKey: '00000000000000000005',
      tags: [],
      tagNames: [],
      createdAtUtc: '2026-03-15T00:00:00Z',
      updatedAtUtc: '2026-03-15T00:01:00Z'
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
      createdAtUtc: '2026-03-15T00:00:00Z',
      updatedAtUtc: '2026-03-15T00:02:00Z'
    };
    api.saveCard.mockResolvedValue(ok(updated));

    await store.saveCard(101, 'Task A+', 'Updated', ['Bug'], 1, 1);

    expect(store.getCardById(101)?.title).toBe('Task A+');
    expect(store.getCardById(101)?.tagNames).toEqual(['Bug']);
    expect(api.saveCard).toHaveBeenCalledWith(1, 101, 'Task A+', 'Updated', ['Bug'], 1, 1);
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

  it('sets feedback error when API returns failure', async () => {
    const store = useCardStore();
    const feedback = useUiFeedbackStore();
    store.replaceBoardCards(1, makeBoard().columns);

    const apiError: AppError = {
      kind: 'api',
      message: 'Card create failed.'
    };
    api.createCard.mockResolvedValue(err(apiError));

    await store.createCard(1, 'Bad');

    expect(feedback.errorMessage).toBe('Card create failed.');
  });
});

function makeBoard(id = 1, name = 'Board'): Board {
  return {
    id,
    name,
    description: '',
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
            sortKey: '00000000000000000001',
            tags: [],
            tagNames: [],
            createdAtUtc: '2026-03-15T00:00:00Z',
            updatedAtUtc: '2026-03-15T00:00:00Z'
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
