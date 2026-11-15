import { describe, expect, it } from 'vitest';
import type { BoardColumn, Card } from '../../shared/types/boardTypes';
import { projectSlickMiniMapDraft } from './slickMiniMapDraft';

describe('slick mini map draft placement', () => {
  it('keeps metadata and same-column slick edits in place without changing saved cards', () => {
    const original = card(2, 1, 10);
    const columns = [column(1, [card(1, 1, 10), original, card(3, 1, 20)])];
    const draft = { ...original, title: 'Draft title', cardTypeId: 2, slickId: 20 };
    const result = projectSlickMiniMapDraft(columns, draft, true);
    expect(result[0].cards.map(card => card.id)).toEqual([1, 2, 3]);
    expect(result[0].cards[1]).toBe(draft);
    expect(columns[0].cards[1]).toBe(original);
    expect(original.title).toBe('Card 2');
  });

  it.each([true, false])('previews a cross-column move with cohesion %s', cohesionEnabled => {
    const original = card(1, 1, 10);
    const columns = [column(1, [original]), column(2, [card(2, 2, 20), card(3, 2, 10), card(4, 2, 20)])];
    const draft = { ...original, boardColumnId: 2, slickId: 20 };
    const result = projectSlickMiniMapDraft(columns, draft, cohesionEnabled);
    expect(result[0].cards).toEqual([]);
    expect(result[1].cards.map(card => card.id)).toEqual(cohesionEnabled ? [2, 1, 3, 4] : [1, 2, 3, 4]);
    expect(columns[0].cards).toEqual([original]);
    expect(columns[1].cards.map(card => card.id)).toEqual([2, 3, 4]);
  });

  it('puts moves without an existing destination slick at the start', () => {
    const original = card(1, 1, 10);
    const columns = [column(1, [original]), column(2, [card(2, 2, 20)])];
    const result = projectSlickMiniMapDraft(columns, { ...original, boardColumnId: 2 }, true);
    expect(result[1].cards.map(card => card.id)).toEqual([1, 2]);
    expect(projectSlickMiniMapDraft(columns, null, true)).toBe(columns);
  });
});

function column(id: number, cards: Card[]): BoardColumn {
  return { id, cards, title: `Column ${id}`, sortKey: String(id), createdAtUtc: '', updatedAtUtc: '' };
}

function card(id: number, boardColumnId: number, slickId: number): Card {
  return {
    id, boardColumnId, slickId, slick: null, cardTypeId: 1, cardTypeName: 'Story', cardTypeEmoji: null,
    title: `Card ${id}`, description: '', externalUrl: null, sortKey: String(id), tags: [], tagNames: [],
    completedChecklistItemCount: 0, totalChecklistItemCount: 0, cardCreatedUtc: '', cardUpdatedUtc: ''
  };
}
