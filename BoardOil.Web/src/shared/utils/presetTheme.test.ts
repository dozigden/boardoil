import { describe, expect, it } from 'vitest';
import {
  DEFAULT_PRESET_INDEX,
  getPresetCssValue,
  getSlickPresetCssValue,
  PRESET_TOKEN_COUNT,
  PRESET_TOKENS,
  resolvePresetIndex,
  SLICK_PRESET_TOKENS
} from './presetTheme';

describe('presetTheme', () => {
  it('exposes preset token list with stable CSS var names', () => {
    expect(PRESET_TOKEN_COUNT).toBe(12);
    expect(PRESET_TOKENS).toHaveLength(PRESET_TOKEN_COUNT);
    expect(SLICK_PRESET_TOKENS).toHaveLength(PRESET_TOKEN_COUNT);
    expect(PRESET_TOKENS[0].cssVar).toBe('--bo-preset-0');
    expect(PRESET_TOKENS[11].cssValue).toBe('var(--bo-preset-11)');
    expect(SLICK_PRESET_TOKENS[0].cssVar).toBe('--bo-slick-preset-0');
    expect(SLICK_PRESET_TOKENS[11].cssValue).toBe('var(--bo-slick-preset-11)');
  });

  it('clamps invalid preset index values to default', () => {
    expect(resolvePresetIndex(-1)).toBe(DEFAULT_PRESET_INDEX);
    expect(resolvePresetIndex(200)).toBe(DEFAULT_PRESET_INDEX);
    expect(resolvePresetIndex('oops')).toBe(DEFAULT_PRESET_INDEX);
  });

  it('returns CSS var values for preset backgrounds', () => {
    expect(getPresetCssValue(3)).toBe('var(--bo-preset-3)');
    expect(getPresetCssValue('3')).toBe('var(--bo-preset-3)');
    expect(getSlickPresetCssValue(3)).toBe('var(--bo-slick-preset-3)');
    expect(getSlickPresetCssValue('3')).toBe('var(--bo-slick-preset-3)');
  });
});
