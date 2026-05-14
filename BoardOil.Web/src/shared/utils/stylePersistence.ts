import { PRESET_TOKEN_COUNT } from './presetTheme';
import type {
  BorderMode,
  GradientStyleModel,
  SolidStyleModel,
  StyleModel,
  StylePresentation,
  TextColorMode
} from './styleTypes';

const DEFAULT_TEXT_COLOR = '#111827';
const DEFAULT_BORDER_COLOR = '#D8CDEC';
const HEX_COLOR_REGEX = /^#[0-9A-F]{6}$/u;

type ParsedStyleProperties = {
  textColorMode?: unknown;
  borderMode?: unknown;
  borderColor?: unknown;
  presetIndex?: unknown;
  backgroundColor?: unknown;
  leftColor?: unknown;
  rightColor?: unknown;
  textColor?: unknown;
};

export function deserializeStyle(style: StylePresentation): StyleModel {
  const styleProperties = tryParseStyleProperties(style.stylePropertiesJson);
  if (!styleProperties) {
    console.warn('[BoardOil style-parse fallback]', { reason: 'unparseable_json', styleName: style.styleName });
    return { styleName: 'auto' };
  }

  switch (style.styleName) {
    case 'auto':
      return { styleName: 'auto' };
    case 'presets':
      return parsePresetsStyle(styleProperties, style.styleName);
    case 'solid':
      return parseSolidStyle(styleProperties, style.styleName);
    case 'gradient':
      return parseGradientStyle(styleProperties, style.styleName);
    default:
      console.warn('[BoardOil style-parse fallback]', { reason: 'unknown_style_name', styleName: style.styleName });
      return { styleName: 'auto' };
  }
}

export function serializeStyleModel(styleModel: StyleModel): string {
  switch (styleModel.styleName) {
    case 'auto':
      return '{}';
    case 'presets':
      return JSON.stringify({ presetIndex: styleModel.presetIndex });
    case 'solid':
      return JSON.stringify({
        backgroundColor: styleModel.backgroundColor,
        ...serializeManualOptions(styleModel)
      });
    case 'gradient':
      return JSON.stringify({
        leftColor: styleModel.leftColor,
        rightColor: styleModel.rightColor,
        ...serializeManualOptions(styleModel)
      });
    default:
      return '{}';
  }
}

function parsePresetsStyle(styleProperties: ParsedStyleProperties, styleName: StylePresentation['styleName']): StyleModel {
  const presetIndex = parsePresetIndex(styleProperties.presetIndex);
  if (presetIndex === null) {
    console.warn('[BoardOil style-parse fallback]', { reason: 'invalid_preset_payload', styleName });
    return { styleName: 'auto' };
  }

  return {
    styleName: 'presets',
    presetIndex
  };
}

function parseSolidStyle(styleProperties: ParsedStyleProperties, styleName: StylePresentation['styleName']): StyleModel {
  const backgroundColor = parseHexColorValue(styleProperties.backgroundColor);
  const manual = parseManualOptions(styleProperties);
  if (!backgroundColor || !manual) {
    console.warn('[BoardOil style-parse fallback]', { reason: 'invalid_solid_payload', styleName });
    return { styleName: 'auto' };
  }

  return {
    styleName: 'solid',
    backgroundColor,
    ...manual
  };
}

function parseGradientStyle(styleProperties: ParsedStyleProperties, styleName: StylePresentation['styleName']): StyleModel {
  const leftColor = parseHexColorValue(styleProperties.leftColor);
  const rightColor = parseHexColorValue(styleProperties.rightColor);
  const manual = parseManualOptions(styleProperties);
  if (!leftColor || !rightColor || !manual) {
    console.warn('[BoardOil style-parse fallback]', { reason: 'invalid_gradient_payload', styleName });
    return { styleName: 'auto' };
  }

  return {
    styleName: 'gradient',
    leftColor,
    rightColor,
    ...manual
  };
}

function parseManualOptions(styleProperties: ParsedStyleProperties): Pick<SolidStyleModel, 'textColorMode' | 'borderMode' | 'textColor' | 'borderColor'> | null {
  const textColorMode = parseTextColorMode(styleProperties.textColorMode);
  const borderMode = parseBorderMode(styleProperties.borderMode);
  if (!textColorMode || !borderMode) {
    return null;
  }

  const textColor = textColorMode === 'custom'
    ? parseHexColorValue(styleProperties.textColor)
    : DEFAULT_TEXT_COLOR;
  const borderColor = borderMode === 'custom'
    ? parseHexColorValue(styleProperties.borderColor)
    : DEFAULT_BORDER_COLOR;
  if (!textColor || !borderColor) {
    return null;
  }

  return {
    textColorMode,
    borderMode,
    textColor,
    borderColor
  };
}

function serializeManualOptions(styleModel: SolidStyleModel | GradientStyleModel): Record<string, string> {
  return {
    textColorMode: styleModel.textColorMode,
    borderMode: styleModel.borderMode,
    ...(styleModel.borderMode === 'custom' ? { borderColor: styleModel.borderColor } : {}),
    ...(styleModel.textColorMode === 'custom' ? { textColor: styleModel.textColor } : {})
  };
}

function tryParseStyleProperties(rawJson: string): ParsedStyleProperties | null {
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

function parseTextColorMode(value: unknown): TextColorMode | null {
  return value === 'auto' || value === 'custom' ? value : null;
}

function parseBorderMode(value: unknown): BorderMode | null {
  return value === 'auto' || value === 'custom' || value === 'none' ? value : null;
}

function parsePresetIndex(value: unknown): number | null {
  const parsed = typeof value === 'number' ? value : Number.parseInt(String(value ?? ''), 10);
  if (!Number.isInteger(parsed)) {
    return null;
  }

  if (parsed < 0 || parsed >= PRESET_TOKEN_COUNT) {
    return null;
  }

  return parsed;
}

function parseHexColorValue(value: unknown): string | null {
  if (typeof value !== 'string') {
    return null;
  }

  const candidate = value.trim().toUpperCase();
  return HEX_COLOR_REGEX.test(candidate) ? candidate : null;
}
