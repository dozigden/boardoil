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
  it('keeps dirty false for no-op patches and marks dirty only when asked and changed', () => {
    const model = useCardEditDraft();
    const card = makeCard();

    const initialised = model.initializeDraftFromCard(card);
    expect(initialised).toBe(true);
    expect(model.isDirty.value).toBe(false);

    model.patchDraft({ description: card.description }, true);
    expect(model.isDirty.value).toBe(false);

    model.patchDraft({ title: 'Changed title' });
    expect(model.isDirty.value).toBe(false);

    model.patchDraft({ title: 'Changed title' }, true);
    expect(model.isDirty.value).toBe(false);

    model.patchDraft({ title: 'Changed title 2' }, true);
    expect(model.isDirty.value).toBe(true);
  });

  it('does not reinitialize for the same card id and resets dirty on clear', () => {
    const model = useCardEditDraft();
    const card = makeCard({ id: 33 });

    expect(model.initializeDraftFromCard(card)).toBe(true);
    model.patchDraft({ slickName: 'Roadmap' }, true);
    expect(model.isDirty.value).toBe(true);

    expect(model.initializeDraftFromCard(card)).toBe(false);
    expect(model.cardDraftId.value).toBe(33);

    model.clearDraft();
    expect(model.cardDraft.value).toBeNull();
    expect(model.cardDraftId.value).toBeNull();
    expect(model.isDirty.value).toBe(false);
  });
});
