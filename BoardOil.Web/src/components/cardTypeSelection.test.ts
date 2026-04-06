import { describe, expect, it } from 'vitest';
import { resolveDraftCardTypeId, resolveSelectedCardTypeEmoji } from './cardTypeSelection';

const cardTypes = [
  {
    id: 1,
    name: 'Story',
    emoji: null,
    isSystem: true,
    createdAtUtc: '2026-01-01T00:00:00Z',
    updatedAtUtc: '2026-01-01T00:00:00Z'
  },
  {
    id: 2,
    name: 'Bug',
    emoji: '🐞',
    isSystem: false,
    createdAtUtc: '2026-01-01T00:00:00Z',
    updatedAtUtc: '2026-01-01T00:00:00Z'
  }
];

describe('resolveSelectedCardTypeEmoji', () => {
  it('returns selected card type emoji when card type exists', () => {
    expect(resolveSelectedCardTypeEmoji(2, cardTypes)).toBe('🐞');
  });

  it('returns null when card type is missing', () => {
    expect(resolveSelectedCardTypeEmoji(999, cardTypes)).toBeNull();
  });
});

describe('resolveDraftCardTypeId', () => {
  it('keeps existing card type id when present', () => {
    expect(resolveDraftCardTypeId(9, 1, 2)).toBe(9);
  });

  it('falls back to system card type id when current is null', () => {
    expect(resolveDraftCardTypeId(null, 1, 2)).toBe(1);
  });

  it('falls back to first card type id when system id is unavailable', () => {
    expect(resolveDraftCardTypeId(null, null, 2)).toBe(2);
  });
});
