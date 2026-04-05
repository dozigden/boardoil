import type { TagPresentation, TagStyleName } from '../types/boardTypes';

type TextColorMode = 'auto' | 'custom';

export type TagStyleDraft = {
  styleName: TagStyleName;
  textColorMode: TextColorMode;
  backgroundColor: string;
  leftColor: string;
  rightColor: string;
  textColor: string;
};

const DEFAULT_TEXT_COLOR = '#111827';
const AUTO_TEXT_COLOR_LIGHT = '#FFFFFF';
const AUTO_TEXT_COLOR_DARK = '#111827';

export const DEFAULT_TAG_STYLE_PROPERTIES_JSON = '{"backgroundColor":"#69C1CE","textColorMode":"auto"}';

export function normaliseTagEmojiForRender(rawEmoji: string | null | undefined): string | null {
  const trimmed = rawEmoji?.trim() ?? '';
  return trimmed.length > 0 ? trimmed : null;
}

export function createTagStyleDraft(tag: TagPresentation): TagStyleDraft {
  const styleName: TagStyleName = tag.styleName === 'gradient' ? 'gradient' : 'solid';
  const styleProperties = parseStyleProperties(tag.stylePropertiesJson);
  const textColorMode: TextColorMode = styleProperties.textColorMode === 'custom' ? 'custom' : 'auto';
  const solidColor = styleName === 'solid'
    ? normalizeHexColor(styleProperties.backgroundColor)
    : normalizeHexColor(styleProperties.leftColor);

  return {
    styleName,
    textColorMode,
    backgroundColor: solidColor,
    leftColor: styleName === 'gradient' ? normalizeHexColor(styleProperties.leftColor) : solidColor,
    rightColor: styleName === 'gradient' ? normalizeHexColor(styleProperties.rightColor) : solidColor,
    textColor: styleProperties.textColor ? normalizeHexColor(styleProperties.textColor) : DEFAULT_TEXT_COLOR
  };
}

export function buildStylePropertiesJsonFromDraft(draft: TagStyleDraft): string {
  const payload = draft.styleName === 'solid'
    ? {
      backgroundColor: normalizeHexColor(draft.backgroundColor),
      textColorMode: draft.textColorMode,
      ...(draft.textColorMode === 'custom'
        ? { textColor: normalizeHexColor(draft.textColor) }
        : {})
    }
    : {
      leftColor: normalizeHexColor(draft.leftColor),
      rightColor: normalizeHexColor(draft.rightColor),
      textColorMode: draft.textColorMode,
      ...(draft.textColorMode === 'custom'
        ? { textColor: normalizeHexColor(draft.textColor) }
        : {})
    };

  return JSON.stringify(payload);
}

export function getTagPillStyle(tag: TagPresentation | null): Record<string, string> {
  if (!tag) {
    return {
      background: '#F1EBFB',
      color: '#2B1247',
      borderColor: '#D8CDEC'
    };
  }

  const draft = createTagStyleDraft(tag);
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
    borderColor: toRgba(baseColor, 0.48)
  };
}

type ParsedStyleProperties = {
  textColorMode: TextColorMode;
  backgroundColor: string;
  leftColor: string;
  rightColor: string;
  textColor?: string;
};

function parseStyleProperties(stylePropertiesJson: string): ParsedStyleProperties {
  return JSON.parse(stylePropertiesJson) as ParsedStyleProperties;
}

function normalizeHexColor(value: string): string {
  return value.trim().toUpperCase();
}

function resolveTextColor(draft: TagStyleDraft, baseColor: string): string {
  if (draft.textColorMode === 'custom') {
    return normalizeHexColor(draft.textColor);
  }

  return getAutoTextColor(baseColor);
}

function getAutoTextColor(backgroundHex: string): string {
  const rgb = parseHexColor(backgroundHex);
  const brightness = ((rgb.r * 299) + (rgb.g * 587) + (rgb.b * 114)) / 1000;
  return brightness >= 150 ? AUTO_TEXT_COLOR_DARK : AUTO_TEXT_COLOR_LIGHT;
}

function parseHexColor(hex: string): Rgb {
  const value = hex.slice(1);
  return {
    r: Number.parseInt(value.slice(0, 2), 16),
    g: Number.parseInt(value.slice(2, 4), 16),
    b: Number.parseInt(value.slice(4, 6), 16)
  };
}

function toRgba(hex: string, alpha: number): string {
  const rgb = parseHexColor(hex);
  return `rgba(${rgb.r}, ${rgb.g}, ${rgb.b}, ${alpha})`;
}

type Rgb = {
  r: number;
  g: number;
  b: number;
};
