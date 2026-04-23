import type { CardType } from '../../shared/types/boardTypes';

export function resolveSelectedCardTypeEmoji(
  cardTypeId: number | null,
  cardTypes: CardType[]
) {
  if (cardTypeId === null) {
    return null;
  }

  return cardTypes.find(x => x.id === cardTypeId)?.emoji ?? null;
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
