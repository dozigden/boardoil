import { describe, expect, it } from 'vitest';
import type { BoardColumn } from '../../shared/types/boardTypes';
import {
  buildSelectionGooDescriptors,
  buildSelectionGooMembershipSignature,
  buildSelectionGooStyleSignature
} from './selectionGooAdapter';

describe('selectionGooAdapter', () => {
  it('builds descriptors only for selected cards and uses a shared group for cross-column bridging', () => {
    const columns = makeColumns();
    const selectedCardIds = new Set<number>([101, 201]);

    const descriptors = buildSelectionGooDescriptors(columns, selectedCardIds, 'var(--bo-focus-ring)');

    expect(descriptors).toHaveLength(2);
    expect(descriptors.map(x => x.cardId).sort((left, right) => left - right)).toEqual([101, 201]);
    expect(descriptors.map(x => x.columnId).sort((left, right) => left - right)).toEqual([1, 2]);
    expect(new Set(descriptors.map(x => x.groupKey))).toEqual(new Set(['selection']));
    expect(descriptors.every(x => x.colour === 'var(--bo-focus-ring)')).toBe(true);
  });

  it('returns off signature when selection mode is disabled', () => {
    const columns = makeColumns();
    const selectedCardIds = new Set<number>([101, 201]);

    const signature = buildSelectionGooMembershipSignature(columns, selectedCardIds, false);

    expect(signature).toBe('off');
  });

  it('builds deterministic membership signatures from visible selected cards', () => {
    const columns = makeColumns();
    const selectedCardIds = new Set<number>([201, 101]);

    const signature = buildSelectionGooMembershipSignature(columns, selectedCardIds, true);

    expect(signature).toBe('1:101|2:201');
  });

  it('builds style signature from colour token', () => {
    expect(buildSelectionGooStyleSignature('var(--bo-focus-ring)')).toBe('selection:var(--bo-focus-ring)');
  });
});

function makeColumns(): BoardColumn[] {
  return [
    {
      id: 1,
      title: 'Todo',
      sortKey: '00000000000000000001',
      createdAtUtc: '2026-05-10T00:00:00Z',
      updatedAtUtc: '2026-05-10T00:00:00Z',
      cards: [
        makeCard(101, 1),
        makeCard(102, 1)
      ]
    },
    {
      id: 2,
      title: 'Doing',
      sortKey: '00000000000000000002',
      createdAtUtc: '2026-05-10T00:00:00Z',
      updatedAtUtc: '2026-05-10T00:00:00Z',
      cards: [
        makeCard(201, 2)
      ]
    }
  ];
}

function makeCard(id: number, columnId: number) {
  return {
    id,
    boardColumnId: columnId,
    cardTypeId: 1,
    slickId: null,
    cardTypeName: 'Story',
    cardTypeEmoji: null,
    title: `Card ${id}`,
    description: '',
    externalUrl: null,
    sortKey: `${id}`.padStart(20, '0'),
    tags: [],
    tagNames: [],
    createdAtUtc: '2026-05-10T00:00:00Z',
    updatedAtUtc: '2026-05-10T00:00:00Z'
  };
}
