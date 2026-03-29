import { describe, expect, it } from 'vitest';
import { APP_LAYOUT_ADMIN, APP_LAYOUT_BOARD, APP_LAYOUT_PAGE, resolveAppLayout } from './appLayout';

describe('resolveAppLayout', () => {
  it('defaults to page scrolling', () => {
    expect(resolveAppLayout(undefined)).toBe(APP_LAYOUT_PAGE);
  });

  it('uses board layout when requested', () => {
    expect(resolveAppLayout(APP_LAYOUT_BOARD)).toBe(APP_LAYOUT_BOARD);
  });

  it('uses admin layout when requested', () => {
    expect(resolveAppLayout(APP_LAYOUT_ADMIN)).toBe(APP_LAYOUT_ADMIN);
  });

  it('treats unknown layout values as page scrolling', () => {
    expect(resolveAppLayout('something-else')).toBe(APP_LAYOUT_PAGE);
  });
});
