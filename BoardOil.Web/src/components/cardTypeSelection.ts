import type { CardType } from '../types/boardTypes';

export function resolveSelectedCardTypeEmoji(
  cardTypeId: number | null,
  cardTypes: CardType[],
  fallbackEmoji: string | null
) {
  if (cardTypeId === null) {
    return fallbackEmoji;
  }

  return cardTypes.find(x => x.id === cardTypeId)?.emoji ?? fallbackEmoji;
}

export function resolveDraftCardTypeId(
  currentCardTypeId: number | null,
  systemCardTypeId: number | null,
  firstCardTypeId: number | null
) {
  if (currentCardTypeId !== null) {
    return currentCardTypeId;
  }

  if (systemCardTypeId !== null) {
    return systemCardTypeId;
  }

  return firstCardTypeId;
}
