import { describe, expect, it } from 'vitest';
import type { Card } from '../../shared/types/boardTypes';
import { createCardEditModel } from './cardEditModel';

describe('card edit model', () => {
  it('copies every editable card field without retaining the tag-name array', () => {
    const card: Card = {
      id: 17,
      boardColumnId: 4,
      cardTypeId: 3,
      cardTypeName: 'Story',
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
      cardCreatedUtc: '2026-08-30T12:00:00Z',
      cardUpdatedUtc: '2026-08-30T12:30:00Z'
    };

    const model = createCardEditModel(card);

    expect(model).toEqual({
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
  });
});
