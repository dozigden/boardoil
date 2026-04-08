import type { TagStyleName } from '../types/boardTypes';

export type TextColorMode = 'auto' | 'custom';

export type StylePresentation = {
  styleName: string;
  stylePropertiesJson: string;
};

export type StyleDraft = {
  styleName: TagStyleName;
  textColorMode: TextColorMode;
  backgroundColor: string;
  leftColor: string;
  rightColor: string;
  textColor: string;
};

export type SurfaceStyleOptions = {
  fallbackBackground: string;
  fallbackColor: string;
  fallbackBorderColor: string;
  borderAlpha?: number;
};

const DEFAULT_BACKGROUND_COLOR = '#69C1CE';
const DEFAULT_TEXT_COLOR = '#111827';
const AUTO_TEXT_COLOR_LIGHT = '#FFFFFF';
const AUTO_TEXT_COLOR_DARK = '#111827';

type ParsedStyleProperties = {
  textColorMode?: string;
  backgroundColor?: string;
  leftColor?: string;
  rightColor?: string;
  textColor?: string;
};

export function normaliseEmojiForRender(rawEmoji: string | null | undefined): string | null {
  const trimmed = rawEmoji?.trim() ?? '';
  return trimmed.length > 0 ? trimmed : null;
}

export function createStyleDraft(style: StylePresentation, fallbackStylePropertiesJson: string): StyleDraft {
  const styleName: TagStyleName = style.styleName === 'gradient' ? 'gradient' : 'solid';
  const styleProperties = parseStyleProperties(style.stylePropertiesJson, fallbackStylePropertiesJson);
  const textColorMode: TextColorMode = styleProperties.textColorMode === 'custom' ? 'custom' : 'auto';
  const fallbackSolidColor = styleName === 'solid'
    ? DEFAULT_BACKGROUND_COLOR
    : normalizeHexColor(styleProperties.leftColor, DEFAULT_BACKGROUND_COLOR);
  const solidColor = styleName === 'solid'
    ? normalizeHexColor(styleProperties.backgroundColor, fallbackSolidColor)
    : fallbackSolidColor;
  const leftColor = styleName === 'gradient'
    ? normalizeHexColor(styleProperties.leftColor, solidColor)
    : solidColor;
  const rightColor = styleName === 'gradient'
    ? normalizeHexColor(styleProperties.rightColor, leftColor)
    : solidColor;

  return {
    styleName,
    textColorMode,
    backgroundColor: solidColor,
    leftColor,
    rightColor,
    textColor: normalizeHexColor(styleProperties.textColor, DEFAULT_TEXT_COLOR)
  };
}

export function buildStylePropertiesJsonFromDraft(draft: StyleDraft): string {
  const payload = draft.styleName === 'solid'
    ? {
      backgroundColor: normalizeHexColor(draft.backgroundColor, DEFAULT_BACKGROUND_COLOR),
      textColorMode: draft.textColorMode,
      ...(draft.textColorMode === 'custom'
        ? { textColor: normalizeHexColor(draft.textColor, DEFAULT_TEXT_COLOR) }
        : {})
    }
    : {
      leftColor: normalizeHexColor(draft.leftColor, DEFAULT_BACKGROUND_COLOR),
      rightColor: normalizeHexColor(draft.rightColor, DEFAULT_BACKGROUND_COLOR),
      textColorMode: draft.textColorMode,
      ...(draft.textColorMode === 'custom'
        ? { textColor: normalizeHexColor(draft.textColor, DEFAULT_TEXT_COLOR) }
        : {})
    };

  return JSON.stringify(payload);
}

export function getSurfaceStyle(
  style: StylePresentation | null | undefined,
  options: SurfaceStyleOptions,
  fallbackStylePropertiesJson: string
): Record<string, string> {
  if (!style) {
    return {
      background: options.fallbackBackground,
      color: options.fallbackColor,
      borderColor: options.fallbackBorderColor
    };
  }

  const draft = createStyleDraft(style, fallbackStylePropertiesJson);
  const baseColor = draft.styleName === 'solid'
    ? draft.backgroundColor
    : draft.leftColor;
  const textColor = resolveTextColor(draft, baseColor);
  const background = draft.styleName === 'solid'
    ? draft.backgroundColor
    : `linear-gradient(90deg, ${draft.leftColor}, ${draft.rightColor})`;

  return {
    background,
    color: textColor,
    borderColor: toRgba(baseColor, options.borderAlpha ?? 0.48)
  };
}

function parseStyleProperties(stylePropertiesJson: string, fallbackStylePropertiesJson: string): ParsedStyleProperties {
  const parsed = tryParseJsonObject(stylePropertiesJson);
  if (parsed) {
    return parsed;
  }

  return tryParseJsonObject(fallbackStylePropertiesJson) ?? {};
}

function tryParseJsonObject(rawJson: string): ParsedStyleProperties | null {
  if (typeof rawJson !== 'string' || rawJson.trim().length === 0) {
    return null;
  }

  try {
    const value = JSON.parse(rawJson) as unknown;
    if (!value || typeof value !== 'object' || Array.isArray(value)) {
      return null;
    }

    return value as ParsedStyleProperties;
  } catch {
    return null;
  }
}

function normalizeHexColor(value: string | null | undefined, fallback: string): string {
  const candidate = (value ?? '').trim().toUpperCase();
  return /^#[0-9A-F]{6}$/u.test(candidate) ? candidate : fallback;
}

function resolveTextColor(draft: StyleDraft, baseColor: string): string {
  if (draft.textColorMode === 'custom') {
    return normalizeHexColor(draft.textColor, DEFAULT_TEXT_COLOR);
  }

  return getAutoTextColor(baseColor);
}

function getAutoTextColor(backgroundHex: string): string {
  const rgb = parseHexColor(backgroundHex);
  if (!rgb) {
    return DEFAULT_TEXT_COLOR;
  }

  const brightness = ((rgb.r * 299) + (rgb.g * 587) + (rgb.b * 114)) / 1000;
  return brightness >= 150 ? AUTO_TEXT_COLOR_DARK : AUTO_TEXT_COLOR_LIGHT;
}

function parseHexColor(hex: string): Rgb | null {
  const value = hex.trim().toUpperCase();
  if (!/^#[0-9A-F]{6}$/u.test(value)) {
    return null;
  }

  return {
    r: Number.parseInt(value.slice(1, 3), 16),
    g: Number.parseInt(value.slice(3, 5), 16),
    b: Number.parseInt(value.slice(5, 7), 16)
  };
}

function toRgba(hex: string, alpha: number): string {
  const rgb = parseHexColor(hex);
  if (!rgb) {
    return `rgba(17, 24, 39, ${alpha})`;
  }

  return `rgba(${rgb.r}, ${rgb.g}, ${rgb.b}, ${alpha})`;
}

type Rgb = {
  r: number;
  g: number;
  b: number;
};
