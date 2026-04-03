export function formatColumnCardCount(cardCount: number): string {
  return `${cardCount} card${cardCount === 1 ? '' : 's'}`;
}
