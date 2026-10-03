import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useBoardStore } from './boardStore';
import { useCardStore } from './cardStore';
import { useBoardMembersStore } from './boardMembersStore';
import { useCardTypeStore } from './cardTypeStore';
import { useCommentStore } from './commentStore';
import { useTagStore } from './tagStore';
import { useSlickStore } from './slickStore';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import { useBoardCatalogueStore } from '../../shared/stores/boardCatalogueStore';
import { useCardAttachmentThumbnailStore } from './cardAttachmentThumbnailStore';
import type { AppError } from '../../shared/types/appError';
import type { Board, BoardSummary, Card, CardComment, CardType, Column, Slick, Tag } from '../../shared/types/boardTypes';
import { err, ok } from '../../shared/types/result';
import type { Result } from '../../shared/types/result';
import type { CardAttachment } from '../../shared/types/attachmentTypes';

const api = {
  supportsAttachments: true,
  getCardThumbnails: vi.fn(),
  getCardTypes: vi.fn(),
  getTags: vi.fn(),
  getSlicks: vi.fn(),
  getBoard: vi.fn(),
  getBoards: vi.fn(),
  saveBoard: vi.fn(),
  deleteBoard: vi.fn(),
  getBoardMembers: vi.fn(),
  createColumn: vi.fn(),
  saveColumn: vi.fn(),
  moveColumn: vi.fn(),
  deleteColumn: vi.fn(),
  deleteSlick: vi.fn(),
  deleteTag: vi.fn()
};

const realtime = {
  connect: vi.fn(),
  disconnect: vi.fn()
};
type RealtimeHandlers = {
  onCardCreated: (boardId: number, card: Card) => Promise<unknown> | unknown;
  onCardMoved: (boardId: number, card: Card) => Promise<unknown> | unknown;
  onCardUpdated: (boardId: number, card: Card) => Promise<unknown> | unknown;
  onCardDeleted: (boardId: number, cardId: number) => Promise<unknown> | unknown;
  onCommentCreated: (boardId: number, comment: CardComment) => Promise<unknown> | unknown;
  onAttachmentAdded: (boardId: number, cardId: number, attachment: CardAttachment) => Promise<unknown> | unknown;
  onAttachmentDeleted: (boardId: number, cardId: number, attachmentId: number) => Promise<unknown> | unknown;
  onResync: (boardId: number) => Promise<unknown> | unknown;
  onConnectionWarning?: (message: string) => Promise<unknown> | unknown;
  onConnectionRecovered?: () => Promise<unknown> | unknown;
};
let realtimeHandlers: RealtimeHandlers | null = null;
const systemInfoMessageStore = {
  setMessage: vi.fn(),
  load: vi.fn(async () => true)
};

vi.mock('../../shared/api/boardApi', () => ({
  createBoardApi: () => api
}));

vi.mock('../realtime/boardRealtime', () => ({
  createBoardRealtime: vi.fn(handlers => {
    realtimeHandlers = handlers;
    return realtime;
  })
}));

vi.mock('../../shared/stores/systemInfoMessageStore', () => ({
  useSystemInfoMessageStore: () => systemInfoMessageStore
}));

describe('boardStore', () => {
  afterEach(async () => {
    await useBoardStore().dispose();
    vi.useRealTimers();
  });

  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    realtimeHandlers = null;
    systemInfoMessageStore.setMessage.mockReset();
    systemInfoMessageStore.load.mockClear();
    api.getBoard.mockResolvedValue(ok(makeBoard()));
    api.getBoards.mockResolvedValue(ok([makeBoard(1), makeBoard(2)]));
    api.getBoardMembers.mockResolvedValue(ok([{ userId: 7, displayName: 'Member', userName: 'member', role: 'Owner', profileImageRelativePath: null }]));
    api.getCardThumbnails.mockResolvedValue(ok([]));
    api.getCardTypes.mockResolvedValue(ok([]));
    api.getTags.mockResolvedValue(ok([]));
    api.getSlicks.mockResolvedValue(ok([]));
    api.deleteSlick.mockResolvedValue(ok(undefined));
    api.deleteTag.mockResolvedValue(ok(undefined));
    realtime.connect.mockResolvedValue(undefined);
    realtime.disconnect.mockResolvedValue(undefined);
  });

  it.each(['success', 'failure', 'board change', 'disposal'])('coordinates board save after %s', async outcome => {
    const store = useBoardStore();
    const catalogue = useBoardCatalogueStore();
    await catalogue.loadBoards();
    await store.initialize(1);
    const saved = { ...makeBoard(1), name: 'Renamed board' };
    const pending = deferred<Result<BoardSummary, AppError>>();
    api.saveBoard.mockReturnValueOnce(pending.promise);
    const saving = store.saveBoard(1, saved);
    if (outcome === 'board change') {
      api.getBoard.mockResolvedValueOnce(ok(makeBoard(2, 'Selected board')));
      await store.initialize(2);
    } else if (outcome === 'disposal') {
      await store.dispose();
    }
    pending.resolve(outcome === 'failure'
      ? err({ kind: 'api', message: 'Save failed' })
      : ok(saved));

    expect(await saving).toEqual(outcome === 'failure' ? null : saved);
    expect(catalogue.boards.find(board => board.id === 1)?.name).toBe(outcome === 'failure' ? 'Board' : saved.name);
    if (outcome === 'disposal') {
      expect(store.board).toBeNull();
    } else if (outcome === 'board change') {
      expect(store.board).toMatchObject({ id: 2, name: 'Selected board' });
    } else {
      expect(store.board?.name).toBe(outcome === 'failure' ? 'Board' : saved.name);
      expect(store.board?.columns[0].cards).toHaveLength(1);
    }
  });

  it.each(['success', 'failure', 'board change', 'disposal'])('coordinates board deletion after %s', async outcome => {
    const store = useBoardStore();
    const catalogue = useBoardCatalogueStore();
    await catalogue.loadBoards();
    await store.initialize(1);
    const pending = deferred<Result<void, AppError>>();
    api.deleteBoard.mockReturnValueOnce(pending.promise);
    const deleting = store.deleteBoard(1);
    if (outcome === 'board change') {
      api.getBoard.mockResolvedValueOnce(ok(makeBoard(2, 'Selected board')));
      await store.initialize(2);
    } else if (outcome === 'disposal') {
      await store.dispose();
    }
    realtime.disconnect.mockClear();
    pending.resolve(outcome === 'failure'
      ? err({ kind: 'api', message: 'Delete failed' })
      : ok(undefined));

    expect(await deleting).toBe(outcome !== 'failure');
    expect(catalogue.boards.map(board => board.id)).toEqual(outcome === 'failure' ? [1, 2] : [2]);
    if (outcome === 'success' || outcome === 'disposal') {
      expect(store.board).toBeNull();
      expect(useCardStore().cardsById).toEqual({});
    } else {
      expect(store.board?.id).toBe(outcome === 'board change' ? 2 : 1);
      expect(useCardStore().getCardById(101)).not.toBeNull();
    }
    expect(realtime.disconnect).toHaveBeenCalledTimes(outcome === 'success' ? 1 : 0);
  });

  it('selects the requested board and clears previous data before its snapshot arrives', async () => {
    const store = useBoardStore();
    const members = useBoardMembersStore();
    await store.initialize(1);
    await members.loadMembers(1);
    const pending = deferred<Result<Board, AppError>>();
    api.getBoard.mockReturnValueOnce(pending.promise);

    const feedback = useUiFeedbackStore();
    feedback.setError('Previous board member error.', 'boardMembers');
    const initialization = store.initialize(2);

    expect(feedback.errorMessage).toBe('');
    expect(store.currentBoardId).toBe(2);
    expect(store.board).toBeNull();
    expect(store.isLoadingBoard).toBe(true);
    expect(useCardStore().cardsById).toEqual({});
    expect(members.members).toEqual([]);
    await realtimeHandlers!.onCardUpdated(1, makeBoard().columns[0].cards[0]);
    expect(useCardStore().cardsById).toEqual({});

    pending.resolve(ok(makeBoard(2)));
    expect(await initialization).toBe(true);
    expect(store.board?.id).toBe(2);
    expect(store.isLoadingBoard).toBe(false);
  });

  it.each(['success', 'failure'] as const)('ignores an old snapshot %s while the selected board is still loading', async outcome => {
    const store = useBoardStore();
    const feedback = useUiFeedbackStore();
    const oldSnapshot = deferred<Result<Board, AppError>>();
    const selectedSnapshot = deferred<Result<Board, AppError>>();
    api.getBoard.mockReturnValueOnce(oldSnapshot.promise).mockReturnValueOnce(selectedSnapshot.promise);
    const oldInitialization = store.initialize(1);
    const selectedInitialization = store.initialize(2);
    feedback.setError('Current feedback', 'board');

    oldSnapshot.resolve(outcome === 'success'
      ? ok(makeBoard(1))
      : err({ kind: 'api', message: 'Old board unavailable' }));
    expect(await oldInitialization).toBe(false);
    expect(store.currentBoardId).toBe(2);
    expect(store.board).toBeNull();
    expect(store.isLoadingBoard).toBe(true);
    expect(feedback.errorMessage).toBe('Current feedback');
    expect(api.getTags).not.toHaveBeenCalled();
    expect(realtime.connect).not.toHaveBeenCalled();

    selectedSnapshot.resolve(ok(makeBoard(2)));
    expect(await selectedInitialization).toBe(true);
    expect(store.board?.id).toBe(2);
    expect(store.isLoadingBoard).toBe(false);
  });

  it('clears selection before disconnect finishes and ignores snapshots arriving during disposal', async () => {
    const store = useBoardStore();
    const snapshot = deferred<Result<Board, AppError>>();
    const disconnected = deferred<void>();
    api.getBoard.mockReturnValueOnce(snapshot.promise);
    realtime.disconnect.mockReturnValueOnce(disconnected.promise);
    const initialization = store.initialize(1);
    const disposal = store.dispose();

    expect(store.currentBoardId).toBeNull();
    expect(store.isLoadingBoard).toBe(false);
    snapshot.resolve(ok(makeBoard(1)));
    expect(await initialization).toBe(false);
    expect(store.board).toBeNull();
    expect(realtime.connect).not.toHaveBeenCalled();
    disconnected.resolve();
    await disposal;
  });

  it('does not clear a new board when an earlier disposal finishes', async () => {
    const store = useBoardStore();
    await store.initialize(1);
    const disconnected = deferred<void>();
    realtime.disconnect.mockReturnValueOnce(disconnected.promise);
    const disposal = store.dispose();
    api.getBoard.mockResolvedValueOnce(ok(makeBoard(2)));
    expect(await store.initialize(2)).toBe(true);

    disconnected.resolve();
    await disposal;
    expect(store.currentBoardId).toBe(2);
    expect(store.board?.id).toBe(2);
    expect(useCardStore().activeBoardId).toBe(2);
  });

  it('does not disconnect the current board when an old initialization finishes connecting', async () => {
    const store = useBoardStore();
    const connected = deferred<void>();
    realtime.connect.mockReturnValueOnce(connected.promise);
    const oldInitialization = store.initialize(1);
    await vi.waitFor(() => expect(realtime.connect).toHaveBeenCalledWith(1));
    api.getBoard.mockResolvedValueOnce(ok(makeBoard(2)));
    expect(await store.initialize(2)).toBe(true);

    connected.resolve();
    expect(await oldInitialization).toBe(false);
    expect(realtime.disconnect).not.toHaveBeenCalled();
    expect(store.currentBoardId).toBe(2);
    expect(store.board?.id).toBe(2);
  });

  describe.each(['board change', 'disposal'] as const)('column responses after %s', change => {
    it.each(['create', 'save', 'move', 'delete'] as const)('ignores late %s state changes', async operation => {
      const store = useBoardStore();
      await store.initialize(1);
      const column = { ...makeBoard().columns[0], title: 'Old board response' };
      const pending = deferred<Result<Column | void, AppError>>();
      let request: Promise<void>;
      switch (operation) {
        case 'create':
          api.createColumn.mockReturnValueOnce(pending.promise);
          request = store.createColumn({ title: column.title });
          break;
        case 'save':
          api.saveColumn.mockReturnValueOnce(pending.promise);
          request = store.saveColumn(column.id, { title: column.title });
          break;
        case 'move':
          api.moveColumn.mockReturnValueOnce(pending.promise);
          request = store.moveColumn(column.id, null);
          break;
        case 'delete':
          api.deleteColumn.mockReturnValueOnce(pending.promise);
          request = store.deleteColumn(column.id);
          break;
      }
      expect(store.busy).toBe(true);
      if (change === 'board change') {
        api.getBoard.mockResolvedValueOnce(ok(makeBoard(2, 'Other board')));
        await store.initialize(2);
      } else {
        await store.dispose();
      }
      expect(store.busy).toBe(false);
      const expectedBoard = store.board;
      pending.resolve(ok(operation === 'delete' ? undefined : column));
      await request;
      expect(store.board).toEqual(expectedBoard);
      expect(store.busy).toBe(false);
    });
  });

  it.each(['success', 'failure'] as const)('keeps current column feedback and busy state after old-board %s', async outcome => {
    const store = useBoardStore();
    const feedback = useUiFeedbackStore();
    await store.initialize(1);
    const pending = deferred<Result<Column, AppError>>();
    api.createColumn.mockReturnValueOnce(pending.promise);
    const oldRequest = store.createColumn({ title: 'Old' });
    api.getBoard.mockResolvedValueOnce(ok(makeBoard(2)));
    await store.initialize(2);
    const current = deferred<Result<Column, AppError>>();
    api.createColumn.mockReturnValueOnce(current.promise);
    const currentRequest = store.createColumn({ title: 'Current' });
    feedback.setError('Current feedback', 'board');

    pending.resolve(outcome === 'success'
      ? ok(makeBoard().columns[0])
      : err({ kind: 'api', message: 'Old error' }));
    await oldRequest;
    expect(store.busy).toBe(true);
    expect(feedback.errorMessage).toBe('Current feedback');

    current.resolve(ok({ ...makeBoard().columns[0], title: 'Current' }));
    await currentRequest;
    expect(store.busy).toBe(false);
    expect(feedback.errorMessage).toBe('');
    expect(store.getColumnById(1)?.title).toBe('Current');
  });

  it.each(['success', 'failure', 'board change', 'disposal'])('coordinates tag deletion after %s', async outcome => {
    const store = useBoardStore();
    const cards = useCardStore();
    const tags = useTagStore();
    const tag = makeCatalogues().tags[0]!;
    const otherTag = { ...tag, id: tag.id + 1, name: 'Retained' };
    const board = makeBoard();
    const card = board.columns[0]!.cards[0]!;
    Object.assign(card, { tags: [tag, otherTag], tagNames: [tag.name, otherTag.name] });
    const otherCard = { ...card, id: 102, tags: [otherTag], tagNames: [otherTag.name] };
    board.columns[0]!.cards.push(otherCard);
    api.getBoard.mockResolvedValue(ok(board));
    api.getTags.mockResolvedValue(ok([tag, otherTag]));
    await store.initialize(1);
    const pending = deferred<Result<void, AppError>>();
    api.deleteTag.mockReturnValueOnce(pending.promise);
    const deleting = store.deleteTag(1, tag.id);
    if (outcome === 'board change') {
      api.getBoard.mockResolvedValueOnce(ok({ ...board, id: 2 }));
      await store.initialize(2);
    } else if (outcome === 'disposal') {
      await store.dispose();
    }
    pending.resolve(outcome === 'failure'
      ? err({ kind: 'api', message: 'Deletion failed.' })
      : ok(undefined));

    expect(await deleting).toBe(outcome !== 'failure');
    expect(api.deleteTag).toHaveBeenCalledWith(1, tag.id);
    if (outcome === 'disposal') {
      expect(cards.getCardById(card.id)).toBeNull();
      expect(tags.tags).toEqual([]);
    } else if (outcome === 'success') {
      expect(cards.getCardById(card.id)).toMatchObject({ tags: [otherTag], tagNames: [otherTag.name] });
      expect(cards.getCardById(otherCard.id)).toEqual(otherCard);
      expect(tags.tags).toEqual([otherTag]);
    } else {
      expect(cards.getCardById(card.id)).toEqual(card);
      expect(cards.getCardById(otherCard.id)).toEqual(otherCard);
      expect(tags.tags).toHaveLength(2);
    }
  });

  it.each(['success', 'failure', 'board change', 'disposal'])('coordinates slick deletion after %s', async outcome => {
    const store = useBoardStore();
    const cards = useCardStore();
    const slicks = useSlickStore();
    const slick = makeCatalogues().slicks[0]!;
    const otherSlick = { ...slick, id: slick.id + 1, name: 'Other slick' };
    const board = makeBoard();
    const card = board.columns[0]!.cards[0]!;
    Object.assign(card, { slickId: slick.id, slickName: slick.name, slick });
    const otherCard = { ...card, id: 102, slickId: otherSlick.id, slickName: otherSlick.name, slick: otherSlick };
    board.columns[0]!.cards.push(otherCard);
    api.getBoard.mockResolvedValue(ok(board));
    api.getSlicks.mockResolvedValue(ok([slick, otherSlick]));
    await store.initialize(1);
    const pending = deferred<Result<void, AppError>>();
    api.deleteSlick.mockReturnValueOnce(pending.promise);
    const deleting = store.deleteSlick(slick.id, 1);
    if (outcome === 'board change') {
      api.getBoard.mockResolvedValueOnce(ok({ ...board, id: 2 }));
      await store.initialize(2);
    } else if (outcome === 'disposal') {
      await store.dispose();
    }
    if (outcome === 'failure') {
      pending.resolve(err({ kind: 'api', message: 'Deletion failed.' }));
    } else {
      pending.resolve(ok(undefined));
    }

    expect(await deleting).toBe(outcome !== 'failure');
    expect(api.deleteSlick).toHaveBeenCalledWith(1, slick.id);
    if (outcome === 'disposal') {
      expect(cards.getCardById(card.id)).toBeNull();
      expect(slicks.slicks).toEqual([]);
    } else if (outcome === 'success') {
      expect(cards.getCardById(card.id)).toMatchObject({ slickId: null, slickName: null, slick: null });
      expect(cards.getCardById(otherCard.id)).toEqual(otherCard);
      expect(slicks.slicks).toEqual([otherSlick]);
    } else {
      expect(cards.getCardById(card.id)).toEqual(card);
      expect(cards.getCardById(otherCard.id)).toEqual(otherCard);
      expect(slicks.slicks).toHaveLength(2);
    }
  });

  it('initializes board and connects realtime', async () => {
    const store = useBoardStore();
    const feedback = useUiFeedbackStore();
    expect(store.isLoadingBoard).toBe(false);

    await store.initialize(1);

    expect(api.getBoard).toHaveBeenCalledTimes(1);
    expect(api.getBoard).toHaveBeenCalledWith(1);
    expect(realtime.connect).toHaveBeenCalledWith(1);
    expect(store.isLoadingBoard).toBe(false);
    expect(store.board?.columns.length).toBe(2);
    expect(store.board?.columns[0].cards.map(x => x.id)).toEqual([101]);

    await realtimeHandlers!.onConnectionRecovered?.();

    expect(feedback.toastMessage).toBe('');
  });

  it('accepts realtime comments before opening an editor and after resync', async () => {
    const store = useBoardStore();
    const comments = useCommentStore();
    const comment: CardComment = {
      id: 1,
      cardId: 101,
      authorUserId: null,
      text: 'Realtime comment',
      postedAtUtc: '2026-03-15T00:00:00Z'
    };
    realtime.connect.mockImplementationOnce(async () => {
      await realtimeHandlers!.onCommentCreated(1, comment);
    });

    await store.initialize(1);
    expect(comments.getCommentsForCard(101)).toEqual([comment]);

    await realtimeHandlers!.onResync(1);
    expect(comments.getCommentsForCard(101)).toEqual([]);
    await realtimeHandlers!.onCommentCreated(1, comment);
    expect(comments.getCommentsForCard(101)).toEqual([comment]);

    api.getBoard.mockResolvedValueOnce(ok(makeBoard(2, 'Second board')));
    await store.initialize(2);
    await realtimeHandlers!.onCommentCreated(1, comment);
    expect(comments.getCommentsForCard(101)).toEqual([]);
    const current = { ...comment, id: 2, text: 'Current board comment' };
    await realtimeHandlers!.onCommentCreated(2, current);
    expect(comments.getCommentsForCard(101)).toEqual([current]);

    await store.dispose();
    await realtimeHandlers!.onCommentCreated(2, current);
    expect(comments.commentsByCardId).toEqual({});
  });

  it('orders columns and cards without mutating the board snapshot', async () => {
    const store = useBoardStore();
    const snapshot = makeBoard();
    const backlog = snapshot.columns[0];
    backlog.cards.unshift({
      ...backlog.cards[0],
      id: 102,
      sortKey: '00000000000000000002'
    });
    snapshot.columns.reverse();
    api.getBoard.mockResolvedValueOnce(ok(snapshot));

    await store.initialize(1);

    expect(store.board?.columns.map(column => column.id)).toEqual([1, 2]);
    expect(store.board?.columns[0].cards.map(card => card.id)).toEqual([101, 102]);
    expect(snapshot.columns.map(column => column.id)).toEqual([2, 1]);
    expect(backlog.cards.map(card => card.id)).toEqual([102, 101]);
  });

  it('keeps board loaded and warns when realtime connect fails', async () => {
    const store = useBoardStore();
    const feedback = useUiFeedbackStore();
    realtime.connect.mockRejectedValueOnce(new Error('realtime failed'));

    const initialized = await store.initialize(1);

    expect(initialized).toBe(true);
    expect(store.board?.id).toBe(1);
    expect(store.currentBoardId).toBe(1);
    expect(realtime.disconnect).toHaveBeenCalledTimes(1);
    expect(feedback.warningMessage).toBe('Realtime updates are unavailable. Data may be stale until reconnect.');
  });

  it('loads board catalogues together before showing the board context', async () => {
    const store = useBoardStore();
    const pendingTags = deferred<Result<Tag[], AppError>>();
    api.getTags.mockImplementationOnce(() => pendingTags.promise);

    const initialization = store.initialize(1);
    await vi.waitFor(() => expect(api.getTags).toHaveBeenCalledWith(1));

    expect(api.getCardTypes).toHaveBeenCalledWith(1);
    expect(api.getSlicks).toHaveBeenCalledWith(1);
    expect(store.isLoadingBoard).toBe(true);
    expect(realtime.connect).not.toHaveBeenCalled();

    pendingTags.resolve(ok([]));
    expect(await initialization).toBe(true);
    expect(store.isLoadingBoard).toBe(false);
    expect(realtime.connect).toHaveBeenCalledWith(1);
  });

  it('clears stale board state when requested board fails to load', async () => {
    const store = useBoardStore();
    await store.initialize(1);
    api.getBoard.mockResolvedValueOnce(err({ kind: 'api', message: 'Board not found.' }));

    const initialized = await store.initialize(999);

    expect(initialized).toBe(false);
    expect(store.board).toBeNull();
    expect(store.currentBoardId).toBeNull();
  });

  it.each(['dispose', 'failed initialization', 'failed resync'])('clears all board catalogues after %s', async trigger => {
    const store = useBoardStore();
    const membersStore = useBoardMembersStore();
    const cardTypeStore = useCardTypeStore();
    const tagStore = useTagStore();
    const slickStore = useSlickStore();
    const catalogues = makeCatalogues();
    api.getCardTypes.mockResolvedValueOnce(ok(catalogues.cardTypes));
    api.getTags.mockResolvedValueOnce(ok(catalogues.tags));
    api.getSlicks.mockResolvedValueOnce(ok(catalogues.slicks));
    await store.initialize(1);
    await membersStore.loadMembers(1);
    expect(membersStore.members).toHaveLength(1);
    expect(cardTypeStore.cardTypes).toHaveLength(1);
    expect(tagStore.tags).toHaveLength(1);
    expect(slickStore.slicks).toHaveLength(1);

    if (trigger === 'dispose') {
      await store.dispose();
    } else {
      api.getBoard.mockResolvedValueOnce(err({ kind: 'api', message: 'Board unavailable.' }));
      if (trigger === 'failed initialization') {
        await store.initialize(2);
      } else {
        await realtimeHandlers!.onResync(1);
      }
    }

    expect(store.currentBoardId).toBeNull();
    expect(membersStore.members).toEqual([]);
    expect(membersStore.activeBoardId).toBeNull();
    expect(cardTypeStore.cardTypes).toEqual([]);
    expect(tagStore.tags).toEqual([]);
    expect(slickStore.slicks).toEqual([]);
    expect(cardTypeStore.activeBoardId).toBeNull();
    expect(tagStore.activeBoardId).toBeNull();
    expect(slickStore.activeBoardId).toBeNull();
  });

  it('retains member lookups on same-board refresh and clears them on board change', async () => {
    const store = useBoardStore();
    const membersStore = useBoardMembersStore();
    await store.initialize(1);
    await membersStore.loadMembers(1);

    await realtimeHandlers!.onResync(1);
    expect(membersStore.activeBoardId).toBe(1);
    expect(membersStore.members).toHaveLength(1);

    api.getBoard.mockResolvedValueOnce(ok({ ...makeBoard(), id: 2 }));
    await store.initialize(2);
    expect(membersStore.activeBoardId).toBeNull();
    expect(membersStore.members).toEqual([]);
  });

  it('ignores catalogue responses that arrive after board disposal', async () => {
    const store = useBoardStore();
    const pendingCardTypes = deferred<Result<CardType[], AppError>>();
    const pendingTags = deferred<Result<Tag[], AppError>>();
    const pendingSlicks = deferred<Result<Slick[], AppError>>();
    api.getCardTypes.mockReturnValueOnce(pendingCardTypes.promise);
    api.getTags.mockReturnValueOnce(pendingTags.promise);
    api.getSlicks.mockReturnValueOnce(pendingSlicks.promise);

    const initialization = store.initialize(1);
    await vi.waitFor(() => expect(api.getSlicks).toHaveBeenCalledWith(1));
    await store.dispose();
    const catalogues = makeCatalogues();
    pendingCardTypes.resolve(ok(catalogues.cardTypes));
    pendingTags.resolve(ok(catalogues.tags));
    pendingSlicks.resolve(ok(catalogues.slicks));

    expect(await initialization).toBe(false);
    expect(store.currentBoardId).toBeNull();
    expect(useCardTypeStore().cardTypes).toEqual([]);
    expect(useTagStore().tags).toEqual([]);
    expect(useSlickStore().slicks).toEqual([]);
    expect(useCardTypeStore().activeBoardId).toBeNull();
    expect(useTagStore().activeBoardId).toBeNull();
    expect(useSlickStore().activeBoardId).toBeNull();
    expect(realtime.connect).not.toHaveBeenCalled();
  });

  it('ignores stale load response when board switches quickly', async () => {
    const store = useBoardStore();
    const delayed = deferred<Result<Board, AppError>>();
    api.getBoard
      .mockImplementationOnce(() => delayed.promise)
      .mockResolvedValueOnce(ok(makeBoard(2, 'Board 2')));

    const firstLoad = store.initialize(1);
    const secondLoad = store.initialize(2);
    delayed.resolve(ok(makeBoard(1, 'Board 1')));
    await Promise.all([firstLoad, secondLoad]);

    expect(store.currentBoardId).toBe(2);
    expect(store.board?.id).toBe(2);
    expect(store.board?.name).toBe('Board 2');
    expect(realtime.connect).toHaveBeenCalledTimes(1);
    expect(realtime.connect).toHaveBeenCalledWith(2);
  });

  it('does not replace the new board catalogues when an old catalogue request finishes', async () => {
    const store = useBoardStore();
    const pendingBoardOneTags = deferred<Result<Tag[], AppError>>();
    api.getBoard
      .mockResolvedValueOnce(ok(makeBoard(1, 'Board 1')))
      .mockResolvedValueOnce(ok(makeBoard(2, 'Board 2')));
    api.getTags.mockImplementationOnce(() => pendingBoardOneTags.promise);

    const firstInitialization = store.initialize(1);
    await vi.waitFor(() => expect(api.getTags).toHaveBeenCalledWith(1));
    expect(api.getCardTypes).toHaveBeenCalledWith(1);
    expect(api.getSlicks).toHaveBeenCalledWith(1);

    expect(await store.initialize(2)).toBe(true);
    pendingBoardOneTags.resolve(ok([]));
    expect(await firstInitialization).toBe(false);

    expect(store.currentBoardId).toBe(2);
    expect(useTagStore().activeBoardId).toBe(2);
    expect(useCardTypeStore().activeBoardId).toBe(2);
    expect(useSlickStore().activeBoardId).toBe(2);
  });

  it('ignores in-flight load response after dispose', async () => {
    const store = useBoardStore();
    const delayed = deferred<Result<Board, AppError>>();
    api.getBoard.mockImplementationOnce(() => delayed.promise);

    const pendingInit = store.initialize(1);
    expect(store.isLoadingBoard).toBe(true);
    await store.dispose();
    delayed.resolve(ok(makeBoard(1, 'Board 1')));
    const initialized = await pendingInit;

    expect(initialized).toBe(false);
    expect(store.isLoadingBoard).toBe(false);
    expect(store.board).toBeNull();
    expect(store.currentBoardId).toBeNull();
  });

  it('switches board context on sequential initialize calls', async () => {
    const store = useBoardStore();
    api.getBoard
      .mockResolvedValueOnce(ok(makeBoard(1, 'Board 1')))
      .mockResolvedValueOnce(ok(makeBoard(2, 'Board 2')));

    const firstInitialized = await store.initialize(1);
    const secondInitialized = await store.initialize(2);

    expect(firstInitialized).toBe(true);
    expect(secondInitialized).toBe(true);
    expect(store.currentBoardId).toBe(2);
    expect(store.board?.id).toBe(2);
    expect(store.board?.name).toBe('Board 2');
    expect(realtime.connect).toHaveBeenNthCalledWith(1, 1);
    expect(realtime.connect).toHaveBeenNthCalledWith(2, 2);
  });

  it('ignores stale realtime events when two boards contain the same card id', async () => {
    const store = useBoardStore();
    api.getBoard
      .mockResolvedValueOnce(ok(makeBoard(1, 'Board 1')))
      .mockResolvedValueOnce(ok(makeBoard(2, 'Board 2')));

    await store.initialize(1);
    await store.initialize(2);
    expect(realtimeHandlers).not.toBeNull();
    const boardTwoCard = store.board!.columns[0].cards[0];
    const staleBoardOneCard = {
      ...boardTwoCard,
      title: 'Stale board one update'
    };

    await realtimeHandlers!.onCardUpdated(1, staleBoardOneCard);
    await realtimeHandlers!.onCardDeleted(1, boardTwoCard.id);

    expect(store.board!.columns[0].cards[0].title).toBe('Task A');
    expect(store.board!.columns[0].cards[0].id).toBe(101);

    await realtimeHandlers!.onCardUpdated(2, {
      ...boardTwoCard,
      title: 'Current board update'
    });

    expect(store.board!.columns[0].cards[0].title).toBe('Current board update');
  });

  it('creates a column incrementally without reloading board', async () => {
    const store = useBoardStore();
    await store.initialize(1);

    const created: Column = {
      id: 3,
      title: 'Done',
      sortKey: '00000000000000000030',
      createdAtUtc: '2026-03-15T00:00:00Z',
      updatedAtUtc: '2026-03-15T00:00:00Z'
    };
    api.createColumn.mockResolvedValue(ok(created));

    await store.createColumn({ title: 'Done' });

    expect(api.getBoard).toHaveBeenCalledTimes(1);
    expect(store.board?.columns.map(x => x.title)).toEqual(['Backlog', 'Doing', 'Done']);
  });

  it('saves a column incrementally using typed edit model payload', async () => {
    const store = useBoardStore();
    await store.initialize(1);

    const saved: Column = {
      id: 2,
      title: 'In Progress',
      sortKey: '00000000000000000020',
      createdAtUtc: '2026-03-15T00:00:00Z',
      updatedAtUtc: '2026-03-15T00:02:00Z'
    };
    api.saveColumn.mockResolvedValue(ok(saved));

    await store.saveColumn(2, { title: 'In Progress' });

    expect(api.saveColumn).toHaveBeenCalledWith(1, 2, { title: 'In Progress' });
    expect(store.board?.columns.map(x => x.title)).toEqual(['Backlog', 'In Progress']);
  });

  it('reorders a column incrementally when updated sort key is returned', async () => {
    const store = useBoardStore();
    await store.initialize(1);

    const moved: Column = {
      id: 2,
      title: 'Doing',
      sortKey: '00000000000000000005',
      createdAtUtc: '2026-03-15T00:00:00Z',
      updatedAtUtc: '2026-03-15T00:03:00Z'
    };
    api.moveColumn.mockResolvedValue(ok(moved));

    await store.moveColumn(2, null);

    expect(store.board?.columns.map(x => x.title)).toEqual(['Doing', 'Backlog']);
    expect(api.moveColumn).toHaveBeenCalledWith(1, 2, null);
  });

  it('sets feedback error when API returns failure', async () => {
    const store = useBoardStore();
    const feedback = useUiFeedbackStore();
    await store.initialize(1);

    const apiError: AppError = {
      kind: 'api',
      message: 'Column create failed.'
    };
    api.createColumn.mockResolvedValue(err(apiError));

    await store.createColumn({ title: 'Bad' });

    expect(feedback.errorMessage).toBe('Column create failed.');
  });

  it('reloads tags and slicks when realtime resync is requested', async () => {
    const store = useBoardStore();
    const cardTypeStore = useCardTypeStore();
    const tagStore = useTagStore();
    const slickStore = useSlickStore();
    const loadCardTypesSpy = vi.spyOn(cardTypeStore, 'loadCardTypes').mockResolvedValue(true);
    const loadTagsSpy = vi.spyOn(tagStore, 'loadTags').mockResolvedValue(true);
    const loadSlicksSpy = vi.spyOn(slickStore, 'loadSlicks').mockResolvedValue(true);

    await store.initialize(1);
    expect(realtimeHandlers).not.toBeNull();

    await realtimeHandlers!.onResync(1);

    expect(api.getBoard).toHaveBeenCalledTimes(2);
    expect(loadCardTypesSpy).toHaveBeenCalledTimes(2);
    expect(loadTagsSpy).toHaveBeenCalledTimes(2);
    expect(loadSlicksSpy).toHaveBeenCalledTimes(2);
  });

  it.each(['onCardCreated', 'onCardUpdated', 'onCardMoved'] as const)('%s upserts slicks for the current board', async event => {
    const store = useBoardStore();
    await store.initialize(1);
    const slickStore = useSlickStore();
    slickStore.activeBoardId = 1;
    const slick = {
      id: 7,
      name: 'Realtime slick',
      styleName: 'presets' as const,
      stylePropertiesJson: '{"presetIndex":2}',
      createdAtUtc: '2026-03-15T00:00:00Z',
      updatedAtUtc: '2026-03-15T00:00:00Z'
    };
    const card = { ...makeBoard().columns[0].cards[0], slickId: slick.id, slickName: slick.name, slick };

    await realtimeHandlers![event](2, card);
    expect(api.getSlicks).toHaveBeenCalledTimes(1);
    await realtimeHandlers![event](1, card);

    expect(slickStore.slicks).toEqual([slick]);
    expect(api.getSlicks).toHaveBeenCalledTimes(1);
    expect(store.board?.columns[0].cards[0].slickId).toBe(7);
    expect(api.getBoard).toHaveBeenCalledTimes(1);
  });

  it('applies realtime attachment changes to the card thumbnail projection', async () => {
    const store = useBoardStore();
    await store.initialize(1);
    await vi.waitFor(() => expect(api.getCardThumbnails).toHaveBeenCalledWith(1));
    const thumbnailStore = useCardAttachmentThumbnailStore();
    const addedAttachment: CardAttachment = {
      id: 8,
      originalFileName: 'first.png',
      contentType: 'image/png',
      byteLength: 10,
      createdAtUtc: '2026-03-15T00:00:01Z',
      createdByUserId: 1,
      hasThumbnail: true
    };

    await realtimeHandlers!.onAttachmentAdded(1, 101, addedAttachment);
    expect(thumbnailStore.getForCard(101)?.attachmentId).toBe(8);
    api.getCardThumbnails.mockResolvedValueOnce(ok([
      { cardId: 101, attachmentId: 9, originalFileName: 'second.png', hasThumbnail: true }
    ]));

    await realtimeHandlers!.onAttachmentDeleted(1, 101, 8);

    expect(api.getCardThumbnails).toHaveBeenLastCalledWith(1, [101]);
    expect(thumbnailStore.getForCard(101)?.attachmentId).toBe(9);
  });

  it('refreshes the thumbnail candidate when realtime creates a card', async () => {
    const store = useBoardStore();
    await store.initialize(1);
    await vi.waitFor(() => expect(api.getCardThumbnails).toHaveBeenCalledWith(1));
    const createdCard = {
      ...makeBoard().columns[0].cards[0],
      id: 102,
      title: 'Duplicated card'
    };
    api.getCardThumbnails.mockResolvedValueOnce(ok([
      { cardId: 102, attachmentId: 12, originalFileName: 'copied.png', hasThumbnail: true }
    ]));

    await realtimeHandlers!.onCardCreated(1, createdCard);

    expect(api.getCardThumbnails).toHaveBeenLastCalledWith(1, [102]);
    expect(useCardAttachmentThumbnailStore().getForCard(102)?.attachmentId).toBe(12);
  });

  it('does not reload board-scoped stores when realtime resync board reload fails', async () => {
    const store = useBoardStore();
    const cardTypeStore = useCardTypeStore();
    const tagStore = useTagStore();
    const slickStore = useSlickStore();
    const loadCardTypesSpy = vi.spyOn(cardTypeStore, 'loadCardTypes').mockResolvedValue(true);
    const loadTagsSpy = vi.spyOn(tagStore, 'loadTags').mockResolvedValue(true);
    const loadSlicksSpy = vi.spyOn(slickStore, 'loadSlicks').mockResolvedValue(true);

    await store.initialize(1);
    loadCardTypesSpy.mockClear();
    loadTagsSpy.mockClear();
    loadSlicksSpy.mockClear();
    api.getBoard.mockResolvedValueOnce(err({ kind: 'api', message: 'Board not found.' }));
    expect(realtimeHandlers).not.toBeNull();

    await realtimeHandlers!.onResync(1);

    expect(store.board).toBeNull();
    expect(store.currentBoardId).toBeNull();
    expect(loadCardTypesSpy).not.toHaveBeenCalled();
    expect(loadTagsSpy).not.toHaveBeenCalled();
    expect(loadSlicksSpy).not.toHaveBeenCalled();
    expect(systemInfoMessageStore.load).not.toHaveBeenCalled();
  });

  it('replaces the realtime reconnect warning with a success toast when recovery completes', async () => {
    const store = useBoardStore();
    const feedback = useUiFeedbackStore();

    await store.initialize(1);
    expect(realtimeHandlers).not.toBeNull();

    await realtimeHandlers!.onConnectionWarning?.('Realtime connection lost. Attempting to reconnect…');
    expect(feedback.warningMessage).toBe('Realtime connection lost. Attempting to reconnect…');

    await realtimeHandlers!.onConnectionRecovered?.();
    expect(feedback.warningMessage).toBe('');
    expect(feedback.toastMessage).toBe('Realtime updates restored.');
    expect(feedback.toastTone).toBe('success');

    await realtimeHandlers!.onConnectionWarning?.('Realtime connection lost. Attempting to reconnect…');
    expect(feedback.toastMessage).toBe('');
    expect(feedback.warningMessage).toBe('Realtime connection lost. Attempting to reconnect…');
  });

  it.each<AppError>([
    { kind: 'network', message: 'Connection lost.' },
    { kind: 'http', statusCode: 408, message: 'Request timed out.' },
    { kind: 'http', statusCode: 429, message: 'Too many requests.' },
    { kind: 'http', statusCode: 503, message: 'Temporarily unavailable.' }
  ])('preserves the board through a temporary resync failure: $message', async error => {
    vi.useFakeTimers();
    const store = useBoardStore();
    const feedback = useUiFeedbackStore();
    const catalogues = makeCatalogues();
    api.getCardTypes.mockResolvedValue(ok(catalogues.cardTypes));
    api.getTags.mockResolvedValue(ok(catalogues.tags));
    api.getSlicks.mockResolvedValue(ok(catalogues.slicks));
    await store.initialize(1);
    const originalBoard = store.board;
    api.getBoard.mockResolvedValueOnce(err(error));

    await realtimeHandlers!.onResync(1);
    await realtimeHandlers!.onConnectionRecovered?.();

    expect(store.board).toBe(originalBoard);
    expect(store.isLoadingBoard).toBe(false);
    expect(useCardTypeStore().cardTypes).toEqual(catalogues.cardTypes);
    expect(useTagStore().tags).toEqual(catalogues.tags);
    expect(useSlickStore().slicks).toEqual(catalogues.slicks);
    expect(feedback.warningMessage).toBe('Board updates are delayed. Retrying…');
    expect(feedback.toastMessage).toBe('');
    expect(feedback.errorMessage).toBe('');

    const recoveredBoard = makeBoard();
    recoveredBoard.columns[0]!.cards[0]!.title = 'Updated during recovery';
    api.getBoard.mockResolvedValueOnce(ok(recoveredBoard));
    await vi.advanceTimersByTimeAsync(2_000);

    expect(api.getBoard).toHaveBeenCalledTimes(3);
    expect(store.board?.columns[0]?.cards[0]?.title).toBe('Updated during recovery');
    expect(feedback.warningMessage).toBe('');
    expect(feedback.toastMessage).toBe('Realtime updates restored.');
  });

  it('backs off background retries to ten seconds and resets after recovery', async () => {
    vi.useFakeTimers();
    const store = useBoardStore();
    await store.initialize(1);
    const failure = err<AppError>({ kind: 'network', message: 'Offline.' });
    api.getBoard.mockResolvedValue(failure);
    await realtimeHandlers!.onResync(1);

    let requests = 2;
    for (const delayMs of [2_000, 5_000, 10_000, 10_000]) {
      await vi.advanceTimersByTimeAsync(delayMs - 1);
      expect(api.getBoard).toHaveBeenCalledTimes(requests);
      await vi.advanceTimersByTimeAsync(1);
      requests++;
      expect(api.getBoard).toHaveBeenCalledTimes(requests);
      expect(store.currentBoardId).toBe(1);
    }

    api.getBoard.mockResolvedValueOnce(ok(makeBoard()));
    await vi.advanceTimersByTimeAsync(10_000);
    expect(useUiFeedbackStore().warningMessage).toBe('');
    await realtimeHandlers!.onResync(1);
    const requestsBeforeRetry = api.getBoard.mock.calls.length;
    await vi.advanceTimersByTimeAsync(1_999);
    expect(api.getBoard).toHaveBeenCalledTimes(requestsBeforeRetry);
    await vi.advanceTimersByTimeAsync(1);
    expect(api.getBoard).toHaveBeenCalledTimes(requestsBeforeRetry + 1);
  });

  it('refreshes immediately on a new resync event and cancels the scheduled retry', async () => {
    vi.useFakeTimers();
    const store = useBoardStore();
    await store.initialize(1);
    api.getBoard.mockResolvedValueOnce(err({ kind: 'network', message: 'Offline.' }));
    await realtimeHandlers!.onResync(1);

    await realtimeHandlers!.onResync(1);
    expect(api.getBoard).toHaveBeenCalledTimes(3);
    expect(useUiFeedbackStore().warningMessage).toBe('');
    await vi.advanceTimersByTimeAsync(10_000);
    expect(api.getBoard).toHaveBeenCalledTimes(3);
  });

  it.each([401, 403, 404])('clears the board without retrying when resync returns %s', async statusCode => {
    vi.useFakeTimers();
    const store = useBoardStore();
    await store.initialize(1);
    api.getBoard.mockResolvedValueOnce(err({ kind: 'http', statusCode, message: 'Board unavailable.' }));

    await realtimeHandlers!.onResync(1);
    await vi.advanceTimersByTimeAsync(10_000);

    expect(store.board).toBeNull();
    expect(api.getBoard).toHaveBeenCalledTimes(2);
  });

  it.each(['dispose', 'board change'])('cancels a pending resync retry after %s', async trigger => {
    vi.useFakeTimers();
    const store = useBoardStore();
    await store.initialize(1);
    api.getBoard.mockResolvedValueOnce(err({ kind: 'network', message: 'Offline.' }));
    await realtimeHandlers!.onResync(1);

    if (trigger === 'dispose') {
      await store.dispose();
    } else {
      api.getBoard.mockResolvedValueOnce(ok({ ...makeBoard(), id: 2 }));
      await store.initialize(2);
    }
    api.getBoard.mockClear();
    await vi.advanceTimersByTimeAsync(10_000);

    expect(api.getBoard).not.toHaveBeenCalled();
    expect(store.currentBoardId).toBe(trigger === 'dispose' ? null : 2);
  });

  it('ignores a retry response after the board has been disposed', async () => {
    vi.useFakeTimers();
    const store = useBoardStore();
    await store.initialize(1);
    api.getBoard.mockResolvedValueOnce(err({ kind: 'network', message: 'Offline.' }));
    await realtimeHandlers!.onResync(1);
    const retry = deferred<Result<Board, AppError>>();
    api.getBoard.mockReturnValueOnce(retry.promise);
    await vi.advanceTimersByTimeAsync(2_000);

    await store.dispose();
    retry.resolve(ok(makeBoard()));
    await vi.advanceTimersByTimeAsync(10_000);

    expect(store.board).toBeNull();
    expect(api.getBoard).toHaveBeenCalledTimes(3);
  });
});

function makeCatalogues() {
  const definition = {
    id: 1,
    name: 'Example',
    styleName: 'presets' as const,
    stylePropertiesJson: '{"presetIndex":1}',
    createdAtUtc: '2026-03-15T00:00:00Z',
    updatedAtUtc: '2026-03-15T00:00:00Z'
  };
  return {
    cardTypes: [{ ...definition, emoji: null, isSystem: false }],
    tags: [{ ...definition, emoji: null }],
    slicks: [definition]
  };
}

function makeBoard(id = 1, name = 'Board'): Board {
  return {
    id,
    name,
    description: '',
    slickCohesionModeEnabled: true,
    cardAttachmentThumbnailsEnabled: true,
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
            slick: null,
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
            completedChecklistItemCount: 0,
            totalChecklistItemCount: 0,
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
