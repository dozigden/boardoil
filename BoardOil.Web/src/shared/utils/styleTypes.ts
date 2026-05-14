import type { TagStyleName } from '../types/boardTypes';

export type TextColorMode = 'auto' | 'custom';
export type BorderMode = 'auto' | 'custom' | 'none';

export type StylePresentation = {
  styleName: TagStyleName;
  stylePropertiesJson: string;
};

export type AutoStyleModel = {
  styleName: 'auto';
};

export type PresetsStyleModel = {
  styleName: 'presets';
  presetIndex: number;
};

export type SolidStyleModel = {
  styleName: 'solid';
  backgroundColor: string;
  textColorMode: TextColorMode;
  borderMode: BorderMode;
  textColor: string;
  borderColor: string;
};

export type GradientStyleModel = {
  styleName: 'gradient';
  leftColor: string;
  rightColor: string;
  textColorMode: TextColorMode;
  borderMode: BorderMode;
  textColor: string;
  borderColor: string;
};

export type StyleModel = AutoStyleModel | PresetsStyleModel | SolidStyleModel | GradientStyleModel;

export type StyleDraft = {
  styleName: TagStyleName;
  textColorMode: TextColorMode;
  borderMode: BorderMode;
  presetIndex: number;
  backgroundColor: string;
  leftColor: string;
  rightColor: string;
  textColor: string;
  borderColor: string;
};

export type SurfaceStyleOptions = {
  fallbackBackground: string;
  fallbackColor: string;
  fallbackBorderColor: string;
  borderAlpha?: number;
};
