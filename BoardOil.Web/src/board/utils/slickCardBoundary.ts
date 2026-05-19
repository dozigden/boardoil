import type { Card as BoardCardModel } from '../../shared/types/boardTypes';

export type SlickBoundaryType = 'none' | 'with-none' | 'between-slicks';

export function resolveCardBoundaryClass(cards: BoardCardModel[], cardIndex: number): string | null {
  if (cardIndex < 0) {
    return null;
  }

  const currentCard = cards[cardIndex];
  if (!currentCard) {
    return null;
  }

  const classNames: string[] = [];
  const currentHasSlick = currentCard.slickId !== null && currentCard.slickId !== undefined;
  if (cardIndex === 0 && currentHasSlick) {
    classNames.push('card--slick-gap-top');
  }

  if (cardIndex === cards.length - 1 && currentHasSlick) {
    classNames.push('card--slick-gap-bottom');
  }

  const previousCard = cards[cardIndex - 1];
  if (previousCard) {
    const boundaryType = resolveSlickBoundaryType(previousCard, currentCard);
    if (boundaryType === 'between-slicks') {
      classNames.push('card--slick-gap-strong');
    }

    if (boundaryType === 'with-none') {
      classNames.push('card--slick-gap');
    }
  }

  if (classNames.length === 0) {
    return null;
  }

  return classNames.join(' ');
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
