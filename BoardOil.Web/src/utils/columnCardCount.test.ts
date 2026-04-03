import { describe, expect, it } from 'vitest';
import { formatColumnCardCount } from './columnCardCount';

describe('formatColumnCardCount', () => {
  it('uses the singular label for one card', () => {
    expect(formatColumnCardCount(1)).toBe('1 card');
  });

  it('uses the plural label for zero or multiple cards', () => {
    expect(formatColumnCardCount(0)).toBe('0 cards');
    expect(formatColumnCardCount(7)).toBe('7 cards');
  });
});
