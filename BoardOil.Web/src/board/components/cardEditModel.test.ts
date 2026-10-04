import { describe, expect, it } from 'vitest';
import type { Card } from '../../shared/types/boardTypes';
import { areCardEditModelsEqual, cloneCardEditModel, createCardEditModel } from './cardEditModel';

describe('card edit model', () => {
  it('copies every editable card field without retaining the tag-name array', () => {
    const card: Card = {
      id: 17,
      slick: null,
      boardColumnId: 4,
      cardTypeId: 3,
      cardTypeName: 'Story',
      fields: { Points: 0, Note: 'Draft', Due: null },
      cardTypeEmoji: '📙',
      assignedUserId: 9,
      assignedUserDisplayName: 'User',
      slickId: 12,
      slickName: 'Release',
      title: 'Source card',
      description: 'Source description',
      externalUrl: 'https://example.test/source',
      sortKey: '0001',
      tags: [],
      tagNames: ['Feature', 'UI'],
      completedChecklistItemCount: 0,
      totalChecklistItemCount: 0,
      cardCreatedUtc: '2026-08-30T12:00:00Z',
      cardUpdatedUtc: '2026-08-30T12:30:00Z'
    };

    const model = createCardEditModel(card);

    expect(model).toEqual({
      fields: card.fields,
      boardColumnId: 4,
      cardTypeId: 3,
      assignedUserId: 9,
      slickName: 'Release',
      title: 'Source card',
      description: 'Source description',
      externalUrl: 'https://example.test/source',
      tagNames: ['Feature', 'UI']
    });
    expect(model.tagNames).not.toBe(card.tagNames);
    expect(model.fields).not.toBe(card.fields);
    const copy = cloneCardEditModel(model);
    expect(copy.fields).not.toBe(model.fields);
    expect(areCardEditModelsEqual(model, copy)).toBe(true);
    copy.fields!.Points = '1e-400';
    expect(areCardEditModelsEqual(model, copy)).toBe(false);
    expect(model.fields!.Points).toBe(0);
  });
});
