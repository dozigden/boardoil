import { DEFAULT_PRESET_INDEX, resolvePresetIndex } from './presetTheme';
import { deserializeStyle, serializeStyleModel } from './stylePersistence';
import type { StyleDraft, StyleModel, StylePresentation } from './styleTypes';

const DEFAULT_BACKGROUND_COLOR = '#69C1CE';
const DEFAULT_LEFT_COLOR = '#69C1CE';
const DEFAULT_RIGHT_COLOR = '#69C1CE';
const DEFAULT_TEXT_COLOR = '#111827';
const DEFAULT_BORDER_COLOR = '#D8CDEC';

export function createStyleDraft(style: StylePresentation): StyleDraft {
  const styleModel = deserializeStyle(style);
  const defaultDraft = createDefaultDraft();

  switch (styleModel.styleName) {
    case 'auto':
      return {
        ...defaultDraft,
        styleName: 'auto'
      };
    case 'presets':
      return {
        ...defaultDraft,
        styleName: 'presets',
        presetIndex: styleModel.presetIndex
      };
    case 'gradient':
      return {
        ...defaultDraft,
        styleName: 'gradient',
        leftColor: styleModel.leftColor,
        rightColor: styleModel.rightColor,
        textColorMode: styleModel.textColorMode,
        borderMode: styleModel.borderMode,
        textColor: styleModel.textColor,
        borderColor: styleModel.borderColor
      };
    case 'solid':
    default:
      return {
        ...defaultDraft,
        styleName: 'solid',
        backgroundColor: styleModel.backgroundColor,
        leftColor: styleModel.backgroundColor,
        rightColor: styleModel.backgroundColor,
        textColorMode: styleModel.textColorMode,
        borderMode: styleModel.borderMode,
        textColor: styleModel.textColor,
        borderColor: styleModel.borderColor
      };
  }
}

export function createRestrictedStyleDraft(
  style: StylePresentation,
  allowedStyleNames: ReadonlySet<StyleDraft['styleName']>,
  fallbackStyle: StylePresentation
): StyleDraft {
  const draft = createStyleDraft(style);
  if (allowedStyleNames.has(draft.styleName)) {
    return draft;
  }

  return createStyleDraft(fallbackStyle);
}

export function buildStylePropertiesJsonFromDraft(draft: StyleDraft): string {
  return serializeStyleModel(draftToStyleModel(draft));
}

function createDefaultDraft(): StyleDraft {
  return {
    styleName: 'solid',
    textColorMode: 'auto',
    borderMode: 'auto',
    presetIndex: DEFAULT_PRESET_INDEX,
    backgroundColor: DEFAULT_BACKGROUND_COLOR,
    leftColor: DEFAULT_LEFT_COLOR,
    rightColor: DEFAULT_RIGHT_COLOR,
    textColor: DEFAULT_TEXT_COLOR,
    borderColor: DEFAULT_BORDER_COLOR
  };
}

function draftToStyleModel(draft: StyleDraft): StyleModel {
  switch (draft.styleName) {
    case 'auto':
      return { styleName: 'auto' };
    case 'presets':
      return {
        styleName: 'presets',
        presetIndex: resolvePresetIndex(draft.presetIndex)
      };
    case 'gradient':
      return {
        styleName: 'gradient',
        leftColor: draft.leftColor,
        rightColor: draft.rightColor,
        textColorMode: draft.textColorMode,
        borderMode: draft.borderMode,
        textColor: draft.textColor,
        borderColor: draft.borderColor
      };
    case 'solid':
    default:
      return {
        styleName: 'solid',
        backgroundColor: draft.backgroundColor,
        textColorMode: draft.textColorMode,
        borderMode: draft.borderMode,
        textColor: draft.textColor,
        borderColor: draft.borderColor
      };
  }
}
