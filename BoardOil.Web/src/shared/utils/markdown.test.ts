import { describe, expect, it } from 'vitest';
import { normaliseMarkdown } from './markdown';

describe('normaliseMarkdown', () => {
  it('converts bare checkbox lines into markdown task list items', () => {
    const input = '[ ] Open item\n[x] Done item';
    const output = normaliseMarkdown(input);

    expect(output).toBe('- [ ] Open item\n- [x] Done item');
  });

  it('preserves existing markdown task list items', () => {
    const input = '- [ ] Open item\n- [x] Done item';
    const output = normaliseMarkdown(input);

    expect(output).toBe(input);
  });

  it('applies max length after task list normalization', () => {
    const input = '[x] Done item';
    const output = normaliseMarkdown(input, 6);

    expect(output).toBe('- [x] ');
  });
});
