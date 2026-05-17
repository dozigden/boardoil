import type { Card as BoardCardModel } from '../../shared/types/boardTypes';

export type SlickBoundaryType = 'none' | 'with-none' | 'between-slicks';

export function resolveCardBoundaryClass(cards: BoardCardModel[], cardIndex: number): string | null {
  if (cardIndex <= 0) {
    return null;
  }

  const previousCard = cards[cardIndex - 1];
  const currentCard = cards[cardIndex];
  if (!previousCard || !currentCard) {
    return null;
  }

  const boundaryType = resolveSlickBoundaryType(previousCard, currentCard);
  if (boundaryType === 'between-slicks') {
    return 'card--slick-gap-strong';
  }

  if (boundaryType === 'with-none') {
    return 'card--slick-gap';
  }

  return null;
}

export function resolveSlickBoundaryType(previousCard: BoardCardModel, currentCard: BoardCardModel): SlickBoundaryType {
  const previousSlickId = previousCard.slickId ?? null;
  const currentSlickId = currentCard.slickId ?? null;
  if (previousSlickId === currentSlickId) {
    return 'none';
  }

  if (previousSlickId !== null && currentSlickId !== null) {
    return 'between-slicks';
  }

  if (previousSlickId !== null || currentSlickId !== null) {
    return 'with-none';
  }

  return 'none';
}
