import { describe, expect, it } from 'vitest';
import {
  APP_LAYOUT_ADMIN,
  APP_LAYOUT_BOARD_ADMIN,
  APP_LAYOUT_BOARD_WITH_CONVEYOR,
  APP_LAYOUT_STANDARD,
  resolveAppLayout
} from './appLayout';

describe('resolveAppLayout', () => {
  it('defaults to page scrolling', () => {
    expect(resolveAppLayout(undefined)).toBe(APP_LAYOUT_STANDARD);
  });

  it('uses board layout when requested', () => {
    expect(resolveAppLayout(APP_LAYOUT_BOARD_WITH_CONVEYOR)).toBe(APP_LAYOUT_BOARD_WITH_CONVEYOR);
  });

  it('uses admin layout when requested', () => {
    expect(resolveAppLayout(APP_LAYOUT_ADMIN)).toBe(APP_LAYOUT_ADMIN);
  });

  it('uses board admin layout when requested', () => {
    expect(resolveAppLayout(APP_LAYOUT_BOARD_ADMIN)).toBe(APP_LAYOUT_BOARD_ADMIN);
  });

  it('treats unknown layout values as page scrolling', () => {
    expect(resolveAppLayout('something-else')).toBe(APP_LAYOUT_STANDARD);
  });
});
