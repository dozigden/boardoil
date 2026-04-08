import type { TagPresentation } from '../types/boardTypes';
import {
  buildStylePropertiesJsonFromDraft as buildSharedStylePropertiesJsonFromDraft,
  createStyleDraft as createSharedStyleDraft,
  getSurfaceStyle,
  normaliseEmojiForRender,
  type StyleDraft
} from './stylePresentation';

export type TagStyleDraft = StyleDraft;

export const DEFAULT_TAG_STYLE_PROPERTIES_JSON = '{"backgroundColor":"#69C1CE","textColorMode":"auto"}';

export function normaliseTagEmojiForRender(rawEmoji: string | null | undefined): string | null {
  return normaliseEmojiForRender(rawEmoji);
}

export function createTagStyleDraft(tag: TagPresentation): TagStyleDraft {
  return createSharedStyleDraft(tag, DEFAULT_TAG_STYLE_PROPERTIES_JSON);
}

export function buildStylePropertiesJsonFromDraft(draft: TagStyleDraft): string {
  return buildSharedStylePropertiesJsonFromDraft(draft);
}

export function getTagPillStyle(tag: TagPresentation | null): Record<string, string> {
  return getSurfaceStyle(
    tag,
    {
      fallbackBackground: '#F1EBFB',
      fallbackColor: '#2B1247',
      fallbackBorderColor: '#D8CDEC'
    },
    DEFAULT_TAG_STYLE_PROPERTIES_JSON
  );
}
