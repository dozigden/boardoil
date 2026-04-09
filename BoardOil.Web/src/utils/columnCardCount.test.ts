import { describe, expect, it } from 'vitest';
import { formatColumnCardCount } from './columnCardCount';

describe('formatColumnCardCount', () => {
  it('returns the card count as plain numeric text', () => {
    expect(formatColumnCardCount(1)).toBe('1');
  });

  it('supports zero and larger counts', () => {
    expect(formatColumnCardCount(0)).toBe('0');
    expect(formatColumnCardCount(7)).toBe('7');
  });
});
