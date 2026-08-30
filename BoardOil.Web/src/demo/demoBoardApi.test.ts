import { beforeEach, describe, expect, it } from 'vitest';
import { createDemoBoardApi, resetDemoData } from './demoBoardApi';

describe('demoBoardApi', () => {
  beforeEach(() => {
    resetDemoData();
  });

  it('creates, edits, and moves cards entirely in browser-local state', async () => {
    const api = createDemoBoardApi();
    const createResult = await api.createCard(1, {
      boardColumnId: 1,
      title: 'Try the interactive preview',
      description: 'Created entirely in the current browser tab.',
      externalUrl: 'https://example.test/demo-card',
      tagNames: ['UI'],
      cardTypeId: 1,
      assignedUserId: 2,
      slickName: 'Launch polish'
    });
    expect(createResult.ok).toBe(true);
    if (!createResult.ok) {
      return;
    }
    expect(createResult.data).toMatchObject({
      description: 'Created entirely in the current browser tab.',
      externalUrl: 'https://example.test/demo-card',
      tagNames: ['UI'],
      assignedUserId: 2,
      slickName: 'Launch polish'
    });

    const editResult = await api.saveCard(1, createResult.data.id, {
      title: 'Try the polished interactive preview',
      description: 'This change exists only in the current browser tab.',
      externalUrl: null,
      tagNames: ['Feature'],
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
      assignedUserDisplayName: 'Jane Doe',
      tagNames: ['Feature']
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

  it('creates visitor slicks in memory with an id and preset style', async () => {
    const api = createDemoBoardApi();

    const editResult = await api.saveCard(1, 101, {
      title: 'Customer interview highlights',
      description: 'Grouped in the demo session.',
      externalUrl: null,
      tagNames: ['Feature'],
      cardTypeId: 1,
      boardColumnId: 1,
      assignedUserId: null,
      slickName: 'Visitor slick'
    });
    expect(editResult.ok).toBe(true);
    if (!editResult.ok) {
      return;
    }

    expect(editResult.data).toMatchObject({
      slickId: 2,
      slickName: 'Visitor slick'
    });

    const slicksResult = await api.getSlicks(1);
    expect(slicksResult.ok).toBe(true);
    if (!slicksResult.ok) {
      return;
    }

    expect(slicksResult.data).toContainEqual(expect.objectContaining({
      id: 2,
      name: 'Visitor slick',
      styleName: 'presets',
      stylePropertiesJson: '{"presetIndex":0}'
    }));
  });

  it('creates visitor tags with preset styling', async () => {
    const api = createDemoBoardApi();

    const createResult = await api.createTag(1, 'Visitor tag', null);
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

  it('archives and restores cards without a server', async () => {
    const api = createDemoBoardApi();

    const archiveResult = await api.archiveCard(1, 101);
    expect(archiveResult.ok).toBe(true);

    const archivedResult = await api.getArchivedCards(1);
    expect(archivedResult.ok).toBe(true);
    if (!archivedResult.ok) {
      return;
    }
    expect(archivedResult.data.items.map(item => item.id)).toContain(101);

    const restoreResult = await api.unarchiveCard(1, 101);
    expect(restoreResult.ok).toBe(true);

    const boardResult = await api.getBoard(1);
    expect(boardResult.ok).toBe(true);
    if (!boardResult.ok) {
      return;
    }
    expect(boardResult.data.columns.flatMap(column => column.cards).map(card => card.id)).toContain(101);
  });
});
