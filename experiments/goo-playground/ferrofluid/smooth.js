const { drawOrganicGoo } = await import(`../baseline/organic.js?load=${Date.now()}`);
const { HEIGHT } = await import('./physics.js');

// Adapt the shared scene geometry to the saved renderer's padded rectangles.
// Its internal 6px inset leaves the same 8px minimum surround as the baseline.
// Keep the baseline implementation and its saved reach/neck settings unchanged.
export function drawSmooth(ctx, cards, colour) {
  const reach = 36, neck = 44, padding = 14;
  const margin = reach * 4 + 24;
  const rects = cards.map((card, index) => ({
    x: card.x - card.width / 2 - padding,
    y: card.y - card.height / 2 - padding,
    width: card.width + padding * 2,
    height: card.height + padding * 2,
    column: card.column ?? index,
  })).filter(rect => rect.y + rect.height >= 56 - margin && rect.y <= HEIGHT + margin);
  if (!rects.length) return 0;
  return drawOrganicGoo(ctx, rects, reach, neck, colour, false);
}
