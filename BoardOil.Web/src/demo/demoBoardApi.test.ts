import { beforeEach, describe, expect, it } from 'vitest';
import type { BoardApi } from '../shared/api/boardApi';
import { createDemoBoardApi, resetDemoData } from './demoBoardApi';

describe('demoBoardApi', () => {
  beforeEach(() => {
    resetDemoData();
  });

  it('deletes comments without allowing a later edit to recreate them', async () => {
    const api = createDemoBoardApi();
    const cardId = await createTestCard(api, 'Comment deletion');
    const created = await api.createCardComment(1, cardId, 'Remove this comment');
    expect(created.ok).toBe(true);
    if (!created.ok) { return; }

    expect(await api.deleteCardComment(1, cardId, created.data.id)).toEqual({ ok: true, data: undefined });

    expect(await api.getCardComments(1, cardId)).toMatchObject({ ok: true, data: [] });
    expect(await api.updateCardComment(1, cardId, created.data.id, 'Older draft')).toMatchObject({ ok: false });
    expect(await api.deleteCardComment(1, cardId, created.data.id)).toEqual({ ok: true, data: undefined });
    expect(await api.deleteCardComment(1, Number.MAX_SAFE_INTEGER, created.data.id)).toEqual({ ok: true, data: undefined });
  });

  it('edits comments while retaining timestamps and archived edit metadata', async () => {
    const api = createDemoBoardApi();
    const cardId = await createTestCard(api, 'Comment editing');
    const created = await api.createCardComment(1, cardId, 'Original comment');
    expect(created.ok).toBe(true);
    if (!created.ok) { return; }
    const unchanged = await api.updateCardComment(1, cardId, created.data.id, ' Original comment ');
    expect(unchanged).toEqual(created);
    const edited = await api.updateCardComment(1, cardId, created.data.id, 'Edited comment');
    expect(edited.ok).toBe(true);
    if (!edited.ok) { return; }
    expect(edited.data.postedAtUtc).toBe(created.data.postedAtUtc);
    expect(edited.data.editedAtUtc).toBeTruthy();

    await api.archiveCards(1, [cardId]);
    const archived = await api.getArchivedCard(1, cardId);
    expect(archived).toMatchObject({ ok: true, data: { comments: [{ text: 'Edited comment', editedAtUtc: edited.data.editedAtUtc }] } });
    expect(await api.updateCardComment(1, cardId, created.data.id, 'Archived edit')).toMatchObject({ ok: false });
  });

  it('loads preview attachments with thumbnails and embedded images', async () => {
    const api = createDemoBoardApi();
    const boardResult = await api.getBoard(1);
    expect(boardResult.ok).toBe(true);
    if (!boardResult.ok) { return; }

    const inventory = await api.getBoardAttachments(1);
    expect(inventory.ok).toBe(true);
    if (!inventory.ok) { return; }
    expect(inventory.data.items.length).toBeGreaterThan(0);

    const cards = boardResult.data.columns.flatMap(column => column.cards);
    for (const { cardId } of inventory.data.items) {
      const attachments = await api.getAttachments(1, cardId, false);
      expect(attachments).toMatchObject({
        ok: true, data: { items: expect.arrayContaining([expect.objectContaining({ hasThumbnail: true })]) }
      });
      expect(cards.find(card => card.id === cardId)?.description).toContain('![');
    }
  });

  it('stores task counts on description writes and retains them on moves', async () => {
    const api = createDemoBoardApi();
    const model = {
      title: 'Checklist', description: '- [x] Done\n- [ ] Open', boardColumnId: 1,
      cardTypeId: 1, assignedUserId: null, slickName: null, externalUrl: null, tagNames: []
    };
    const created = await api.createCard(1, model);
    expect(created.ok).toBe(true);
    if (!created.ok) { return; }
    expect(created.data).toMatchObject({ completedChecklistItemCount: 1, totalChecklistItemCount: 2 });

    const moved = await api.moveCard(1, created.data.id, 3, null);
    expect(moved).toMatchObject({ ok: true, data: { completedChecklistItemCount: 1, totalChecklistItemCount: 2 } });

    const saved = await api.saveCard(1, created.data.id, { ...model, description: 'No tasks', boardColumnId: 3 });
    expect(saved).toMatchObject({ ok: true, data: { completedChecklistItemCount: 0, totalChecklistItemCount: 0 } });
  });

  it('creates, edits, and moves cards entirely in browser-local state', async () => {
    const api = createDemoBoardApi();
    const createResult = await api.createCard(1, {
      boardColumnId: 1,
      title: 'Try the interactive preview',
      description: 'Created entirely in the current browser tab.',
      externalUrl: 'https://example.test/demo-card',
      tagNames: ['Created tag'],
      cardTypeId: 1,
      assignedUserId: 2,
      slickName: 'Created slick'
    });
    expect(createResult.ok).toBe(true);
    if (!createResult.ok) {
      return;
    }
    expect(createResult.data).toMatchObject({
      description: 'Created entirely in the current browser tab.',
      externalUrl: 'https://example.test/demo-card',
      tagNames: ['Created tag'],
      assignedUserId: 2,
      slickName: 'Created slick'
    });

    const editResult = await api.saveCard(1, createResult.data.id, {
      title: 'Try the polished interactive preview',
      description: 'This change exists only in the current browser tab.',
      externalUrl: null,
      tagNames: ['Edited tag'],
      cardTypeId: 1,
      boardColumnId: 1,
      assignedUserId: 1,
      slickName: null
    });
    expect(editResult.ok).toBe(true);

    const moveResult = await api.moveCard(1, createResult.data.id, 3, null);
    expect(moveResult.ok).toBe(true);

    const boardResult = await api.getBoard(1);
    expect(boardResult.ok).toBe(true);
    if (!boardResult.ok) {
      return;
    }

    const movedCard = boardResult.data.columns
      .flatMap(column => column.cards)
      .find(card => card.id === createResult.data.id);
    expect(movedCard).toMatchObject({
      title: 'Try the polished interactive preview',
      boardColumnId: 3,
      assignedUserDisplayName: 'Doz',
      tagNames: ['Edited tag']
    });
  });

  it('creates new cards at the top of their column', async () => {
    const api = createDemoBoardApi();
    const initialBoardResult = await api.getBoard(1);
    expect(initialBoardResult.ok).toBe(true);
    if (!initialBoardResult.ok) {
      return;
    }

    const initialSortKey = initialBoardResult.data.columns.find(column => column.id === 1)?.cards[0]?.sortKey;
    expect(initialSortKey).toBeDefined();
    if (!initialSortKey) {
      return;
    }

    const createResult = await api.createCard(1, {
      boardColumnId: 1,
      title: 'New leading card',
      description: '',
      externalUrl: null,
      tagNames: [],
      cardTypeId: 1,
      assignedUserId: null,
      slickName: null
    });
    expect(createResult.ok).toBe(true);
    if (!createResult.ok) {
      return;
    }
    expect(createResult.data.sortKey < initialSortKey).toBe(true);

    const boardResult = await api.getBoard(1);
    expect(boardResult.ok).toBe(true);
    if (!boardResult.ok) {
      return;
    }

    const targetColumn = boardResult.data.columns.find(column => column.id === 1);
    expect(targetColumn?.cards[0]?.id).toBe(createResult.data.id);
  });

  it('returns a moved-card sort key that places it before the first stale client card', async () => {
    const api = createDemoBoardApi();
    const movedCardId = await createTestCard(api, 'Card to move');
    const leadingCardId = await createTestCard(api, 'Leading card');
    const initialBoardResult = await api.getBoard(1);
    expect(initialBoardResult.ok).toBe(true);
    if (!initialBoardResult.ok) {
      return;
    }

    const firstColumn = initialBoardResult.data.columns.find(column => column.id === 1);
    const originalFirstCard = firstColumn?.cards[0];
    expect(originalFirstCard?.id).toBe(leadingCardId);
    if (!originalFirstCard) {
      return;
    }

    const moveResult = await api.moveCard(1, movedCardId, 1, null);
    expect(moveResult.ok).toBe(true);
    if (!moveResult.ok) {
      return;
    }

    expect(moveResult.data.sortKey < originalFirstCard.sortKey).toBe(true);

    const updatedBoardResult = await api.getBoard(1);
    expect(updatedBoardResult.ok).toBe(true);
    if (!updatedBoardResult.ok) {
      return;
    }

    const firstCards = updatedBoardResult.data.columns.find(column => column.id === 1)?.cards.slice(0, 2);
    expect(firstCards?.map(card => card.id)).toEqual([movedCardId, leadingCardId]);
  });

  it('creates visitor slicks in memory with an id and preset style', async () => {
    const api = createDemoBoardApi();
    const title = 'Slick card';
    const cardId = await createTestCard(api, title);

    const editResult = await api.saveCard(1, cardId, {
      title,
      description: 'Grouped in the demo session.',
      externalUrl: null,
      tagNames: [],
      cardTypeId: 1,
      boardColumnId: 1,
      assignedUserId: null,
      slickName: 'Visitor slick'
    });
    expect(editResult.ok).toBe(true);
    if (!editResult.ok) {
      return;
    }

    const slickId = editResult.data.slickId;
    expect(slickId).toEqual(expect.any(Number));
    expect(editResult.data).toMatchObject({
      slickName: 'Visitor slick',
      slick: {
        id: slickId,
        name: 'Visitor slick',
        styleName: 'presets',
        stylePropertiesJson: '{"presetIndex":0}'
      }
    });

    const slicksResult = await api.getSlicks(1);
    expect(slicksResult.ok).toBe(true);
    if (!slicksResult.ok) {
      return;
    }

    expect(slicksResult.data).toContainEqual(expect.objectContaining({
      id: slickId,
      name: 'Visitor slick',
      styleName: 'presets',
      stylePropertiesJson: '{"presetIndex":0}'
    }));
  });

  it('creates visitor tags with preset styling', async () => {
    const api = createDemoBoardApi();

    const createResult = await api.createTag(1, { name: 'Visitor tag', emoji: null });
    expect(createResult.ok).toBe(true);
    if (!createResult.ok) {
      return;
    }

    expect(createResult.data).toMatchObject({
      name: 'Visitor tag',
      styleName: 'presets',
      stylePropertiesJson: '{"presetIndex":1}'
    });

    const defaultStyleResult = await api.getTagCreateDefaultStyle(1);
    expect(defaultStyleResult).toEqual({
      ok: true,
      data: {
        styleName: 'presets',
        stylePropertiesJson: '{"presetIndex":1}'
      }
    });
  });

  it('creates complete visitor tags and rejects explicit-style duplicates without changing them', async () => {
    const api = createDemoBoardApi();
    const model = {
      name: 'Visitor tag',
      emoji: '🚀',
      styleName: 'solid' as const,
      stylePropertiesJson: '{"backgroundColor":"#123456","textColorMode":"auto","borderMode":"none"}'
    };

    const created = await api.createTag(1, model);
    expect(created).toMatchObject({ ok: true, data: model });
    expect(await api.createTag(1, { ...model, name: 'VISITOR TAG', styleName: 'auto', stylePropertiesJson: '{}' }))
      .toMatchObject({ ok: false, error: { validationErrors: { name: ["Tag 'VISITOR TAG' already exists."] } } });
    expect(await api.createTag(1, { name: 'VISITOR TAG' })).toEqual(created);
  });

  it('does not create a visitor tag with an incomplete style', async () => {
    const api = createDemoBoardApi();
    const before = await api.getTags(1);

    expect(await api.createTag(1, { name: 'Incomplete', styleName: 'auto' })).toMatchObject({ ok: false });
    expect(await api.getTags(1)).toEqual(before);
  });

  it('archives and restores cards without a server', async () => {
    const api = createDemoBoardApi();
    const cardId = await createTestCard(api, 'Archive and restore');

    const archiveResult = await api.archiveCards(1, [cardId]);
    expect(archiveResult.ok).toBe(true);

    const archivedResult = await api.getArchivedCards(1);
    expect(archivedResult.ok).toBe(true);
    if (!archivedResult.ok) {
      return;
    }
    expect(archivedResult.data.items.map(item => item.id)).toContain(cardId);

    const restoreResult = await api.unarchiveCard(1, cardId);
    expect(restoreResult.ok).toBe(true);

    const boardResult = await api.getBoard(1);
    expect(boardResult.ok).toBe(true);
    if (!boardResult.ok) {
      return;
    }
    expect(boardResult.data.columns.flatMap(column => column.cards).map(card => card.id)).toContain(cardId);
  });
});

async function createTestCard(api: BoardApi, title: string) {
  const result = await api.createCard(1, {
    title, description: '', boardColumnId: 1, cardTypeId: 1,
    assignedUserId: null, slickName: null, externalUrl: null, tagNames: []
  });
  if (!result.ok) {
    throw new Error(result.error.message);
  }
  return result.data.id;
}
