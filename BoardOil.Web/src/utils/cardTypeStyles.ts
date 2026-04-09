import type { CardType, TagStyleName } from '../types/boardTypes';
import {
  buildStylePropertiesJsonFromDraft as buildSharedStylePropertiesJsonFromDraft,
  createStyleDraft as createSharedStyleDraft,
  getSurfaceStyle,
  normaliseEmojiForRender,
  type StyleDraft
} from './stylePresentation';

export type CardTypeStyleDraft = StyleDraft;

export const DEFAULT_CARD_TYPE_STYLE_NAME: TagStyleName = 'solid';
export const DEFAULT_CARD_TYPE_STYLE_PROPERTIES_JSON = '{"backgroundColor":"#FFFFFF","textColorMode":"auto","borderMode":"auto"}';

export function normaliseCardTypeEmojiForRender(rawEmoji: string | null | undefined): string | null {
  return normaliseEmojiForRender(rawEmoji);
}

export function createCardTypeStyleDraft(cardType: Pick<CardType, 'styleName' | 'stylePropertiesJson'>): CardTypeStyleDraft {
  return createSharedStyleDraft(cardType, DEFAULT_CARD_TYPE_STYLE_PROPERTIES_JSON);
}

export function buildStylePropertiesJsonFromDraft(draft: CardTypeStyleDraft): string {
  return buildSharedStylePropertiesJsonFromDraft(draft);
}

export function getCardSurfaceStyle(cardType: Pick<CardType, 'styleName' | 'stylePropertiesJson'> | null): Record<string, string> {
  return getSurfaceStyle(
    cardType,
    {
      fallbackBackground: 'var(--bo-surface-base)',
      fallbackColor: 'inherit',
      fallbackBorderColor: 'var(--bo-border-soft)',
      borderAlpha: 0.35
    },
    DEFAULT_CARD_TYPE_STYLE_PROPERTIES_JSON
  );
}
