import type { TagPresentation } from '../types/boardTypes';
import {
  buildStylePropertiesJsonFromDraft as buildSharedStylePropertiesJsonFromDraft,
  createStyleDraft as createSharedStyleDraft
} from './styleDraftAdapter';
import { getSemanticStyleClasses, getSurfaceStyle } from './styleRenderer';
import { normaliseEmojiForRender } from './styleFormatting';
import type { StyleDraft } from './styleTypes';

export type TagStyleDraft = StyleDraft;

export const DEFAULT_TAG_STYLE_PROPERTIES_JSON = '{"presetIndex":2}';

export function normaliseTagEmojiForRender(rawEmoji: string | null | undefined): string | null {
  return normaliseEmojiForRender(rawEmoji);
}

export function createTagStyleDraft(tag: TagPresentation): TagStyleDraft {
  return createSharedStyleDraft(tag);
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
    }
  );
}

export function getTagPillClassList(tag: TagPresentation | null): string[] {
  return getSemanticStyleClasses(tag, 'tag');
}
