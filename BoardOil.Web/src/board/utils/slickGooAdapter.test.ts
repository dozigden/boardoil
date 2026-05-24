import { describe, expect, it } from 'vitest';
import type { BoardColumn, Slick } from '../../shared/types/boardTypes';
import {
  buildSlickGooDescriptors,
  buildSlickGooMembershipSignature,
  buildSlickGooStyleSignature
} from './slickGooAdapter';

describe('slickGooAdapter', () => {
  it('builds descriptors only for cards with slick membership and uses stable group keys', () => {
    const columns = makeColumns();
    const slicksById = new Map<number, Slick>([
      [10, makeSlick(10, 'solid', '{"backgroundColor":"#AABBCC","textColorMode":"auto","borderMode":"auto"}')],
      [20, makeSlick(20, 'presets', '{"presetIndex":4}')]
    ]);

    const descriptors = buildSlickGooDescriptors(columns, slicksById);

    expect(descriptors).toHaveLength(2);
    expect(descriptors.map(x => x.cardId).sort((a, b) => a - b)).toEqual([101, 201]);
    expect(descriptors.map(x => x.groupKey).sort()).toEqual(['slick-10', 'slick-20']);
  });

  it('uses styled background for solid and slick preset css variable for presets style', () => {
    const columns = makeColumns();
    const slicksById = new Map<number, Slick>([
      [10, makeSlick(10, 'solid', '{"backgroundColor":"#AABBCC","textColorMode":"auto","borderMode":"auto"}')],
      [20, makeSlick(20, 'presets', '{"presetIndex":4}')]
    ]);

    const descriptors = buildSlickGooDescriptors(columns, slicksById);
    const solidDescriptor = descriptors.find(x => x.cardId === 101);
    const presetDescriptor = descriptors.find(x => x.cardId === 201);

    expect(solidDescriptor?.colour).toBe('#AABBCC');
    expect(presetDescriptor?.colour).toBe('var(--bo-slick-preset-4)');
  });

  it('builds deterministic membership signatures from card memberships', () => {
    const columns = makeColumns();
    const signature = buildSlickGooMembershipSignature(columns);
    expect(signature).toBe('101:10:1|201:20:2');
  });

  it('changes membership signature when a slick card moves columns', () => {
    const columns = makeColumns();
    const movedColumns = columns.map(column => ({
      ...column,
      cards: column.id === 1
        ? column.cards.filter(card => card.id !== 101)
        : [makeCard(101, 2, 10), ...column.cards]
    }));

    const initial = buildSlickGooMembershipSignature(columns);
    const moved = buildSlickGooMembershipSignature(movedColumns);

    expect(initial).toBe('101:10:1|201:20:2');
    expect(moved).toBe('101:10:2|201:20:2');
  });

  it('builds deterministic style signatures sorted by slick id', () => {
    const styleSignature = buildSlickGooStyleSignature([
      makeSlick(20, 'presets', '{"presetIndex":4}'),
      makeSlick(10, 'solid', '{"backgroundColor":"#AABBCC","textColorMode":"auto","borderMode":"auto"}')
    ]);

    expect(styleSignature).toBe(
      '10:solid:{"backgroundColor":"#AABBCC","textColorMode":"auto","borderMode":"auto"}|20:presets:{"presetIndex":4}'
    );
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
        makeCard(101, 1, 10),
        makeCard(102, 1, null)
      ]
    },
    {
      id: 2,
      title: 'Doing',
      sortKey: '00000000000000000002',
      createdAtUtc: '2026-05-10T00:00:00Z',
      updatedAtUtc: '2026-05-10T00:00:00Z',
      cards: [
        makeCard(201, 2, 20)
      ]
    }
  ];
}

function makeCard(id: number, columnId: number, slickId: number | null) {
  return {
    id,
    boardColumnId: columnId,
    cardTypeId: 1,
    slickId,
    cardTypeName: 'Story',
    cardTypeEmoji: null,
    title: `Card ${id}`,
    description: '',
    sortKey: `${id}`.padStart(20, '0'),
    tags: [],
    tagNames: [],
    createdAtUtc: '2026-05-10T00:00:00Z',
    updatedAtUtc: '2026-05-10T00:00:00Z'
  };
}

function makeSlick(id: number, styleName: Slick['styleName'], stylePropertiesJson: string): Slick {
  return {
    id,
    name: `Slick ${id}`,
    styleName,
    stylePropertiesJson,
    createdAtUtc: '2026-05-10T00:00:00Z',
    updatedAtUtc: '2026-05-10T00:00:00Z'
  };
}
