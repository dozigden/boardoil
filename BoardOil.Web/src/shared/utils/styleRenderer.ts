import { deserializeStyle } from './stylePersistence';
import type { BorderMode, StylePresentation, SurfaceStyleOptions, TextColorMode } from './styleTypes';

const DEFAULT_TEXT_COLOR = '#111827';
const AUTO_TEXT_COLOR_LIGHT = '#FFFFFF';
const AUTO_TEXT_COLOR_DARK = '#111827';
const HEX_COLOR_REGEX = /^#[0-9A-F]{6}$/u;

export function getSurfaceStyle(
  style: StylePresentation | null | undefined,
  options: SurfaceStyleOptions
): Record<string, string> {
  if (!style) {
    return {
      background: options.fallbackBackground,
      color: options.fallbackColor,
      borderColor: options.fallbackBorderColor
    };
  }

  const styleModel = deserializeStyle(style);
  switch (styleModel.styleName) {
    case 'auto':
    case 'presets':
      return {};
    case 'gradient': {
      const baseColor = styleModel.leftColor;
      return {
        background: `linear-gradient(90deg, ${styleModel.leftColor}, ${styleModel.rightColor})`,
        color: resolveManualTextColor(styleModel.textColorMode, styleModel.textColor, baseColor),
        borderColor: resolveManualBorderColor(styleModel.borderMode, styleModel.borderColor, baseColor, options)
      };
    }
    case 'solid':
    default: {
      const baseColor = styleModel.backgroundColor;
      return {
        background: styleModel.backgroundColor,
        color: resolveManualTextColor(styleModel.textColorMode, styleModel.textColor, baseColor),
        borderColor: resolveManualBorderColor(styleModel.borderMode, styleModel.borderColor, baseColor, options)
      };
    }
  }
}

export function getSemanticStyleClasses(
  style: StylePresentation | null | undefined,
  scope: 'tag' | 'card' | 'slick'
): string[] {
  if (!style) {
    return [];
  }

  const styleModel = deserializeStyle(style);
  switch (styleModel.styleName) {
    case 'auto':
      return [`bo-${scope}-style-auto`];
    case 'presets':
      return [`bo-${scope}-style-presets`, `bo-${scope}-style-presets-${styleModel.presetIndex}`];
    case 'solid':
    case 'gradient':
    default:
      return [];
  }
}

function resolveManualTextColor(textColorMode: TextColorMode, textColor: string, baseColor: string): string {
  if (textColorMode === 'custom') {
    return textColor;
  }

  return getAutoTextColor(baseColor);
}

function resolveManualBorderColor(
  borderMode: BorderMode,
  borderColor: string,
  baseColor: string,
  options: SurfaceStyleOptions
): string {
  if (borderMode === 'none') {
    return 'transparent';
  }

  if (borderMode === 'custom') {
    return borderColor;
  }

  const rgb = parseHexColor(baseColor);
  if (!rgb) {
    return options.fallbackBorderColor;
  }

  const lightness = ((rgb.r * 299) + (rgb.g * 587) + (rgb.b * 114)) / 1000;
  if (lightness >= 220) {
    return options.fallbackBorderColor;
  }

  return toRgba(baseColor, options.borderAlpha ?? 0.48);
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
  if (!HEX_COLOR_REGEX.test(value)) {
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
