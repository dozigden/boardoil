export const PRESET_TOKEN_COUNT = 8;
export const DEFAULT_PRESET_INDEX = 2;

export type PresetToken = {
  index: number;
  cssVar: string;
  cssValue: string;
};

export const PRESET_TOKENS: PresetToken[] = Array.from({ length: PRESET_TOKEN_COUNT }, (_, index) => ({
  index,
  cssVar: `--bo-preset-${index}`,
  cssValue: `var(--bo-preset-${index})`
}));

export function resolvePresetIndex(value: unknown): number {
  const rawValue = typeof value === 'number' ? value : Number.parseInt(String(value ?? ''), 10);
  if (!Number.isFinite(rawValue)) {
    return DEFAULT_PRESET_INDEX;
  }

  if (rawValue < 0 || rawValue >= PRESET_TOKEN_COUNT) {
    return DEFAULT_PRESET_INDEX;
  }

  return Math.floor(rawValue);
}

export function getPresetCssValue(index: unknown): string {
  return PRESET_TOKENS[resolvePresetIndex(index)].cssValue;
}
