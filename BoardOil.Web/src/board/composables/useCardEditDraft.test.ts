import { describe, expect, it } from 'vitest';
import { useCardEditDraft } from './useCardEditDraft';
import type { Card } from '../../shared/types/boardTypes';

function makeCard(overrides?: Partial<Card>): Card {
  return {
    id: 17,
    boardColumnId: 3,
    cardTypeId: 4,
    slickId: null,
    slickName: null,
    cardTypeName: 'Task',
    cardTypeEmoji: null,
    assignedUserId: null,
    assignedUserName: null,
    assignedUserImageRelativePath: null,
    title: 'Initial title',
    description: 'Initial description',
    sortKey: '0001',
    tags: [],
    tagNames: ['alpha'],
    createdAtUtc: '2026-01-01T00:00:00Z',
    updatedAtUtc: '2026-01-01T00:00:00Z',
    ...overrides
  };
}

describe('useCardEditDraft', () => {
  it('keeps dirty false for system patches and marks dirty for user changes', () => {
    const model = useCardEditDraft();
    const card = makeCard();

    const initialised = model.initializeDraftFromCard(card);
    expect(initialised).toBe(true);
    expect(model.isDirty.value).toBe(false);

    model.patchFromSystem({ description: 'System-normalised description' });
    expect(model.isDirty.value).toBe(false);

    model.patchFromSystem({ description: card.description });
    expect(model.isDirty.value).toBe(false);

    model.patchFromUser({ title: 'Changed title' });
    expect(model.isDirty.value).toBe(true);
  });

  it('does not reinitialize for the same card id and resets dirty on clear', () => {
    const model = useCardEditDraft();
    const card = makeCard({ id: 33 });

    expect(model.initializeDraftFromCard(card)).toBe(true);
    model.patchFromUser({ slickName: 'Roadmap' });
    expect(model.isDirty.value).toBe(true);

    expect(model.initializeDraftFromCard(card)).toBe(false);
    expect(model.cardDraftId.value).toBe(33);

    model.clearDraft();
    expect(model.cardDraft.value).toBeNull();
    expect(model.cardDraftId.value).toBeNull();
    expect(model.isDirty.value).toBe(false);
  });
});
