import type { GooCard } from './types';

type Samples = { signature: string; values: Float64Array };
export class LiquidCardSamples {
  private cards = new Map<number, Samples>();
  private used = new Set<number>();

  begin() { this.used.clear(); }
  get(card: GooCard, originX: number, originY: number, width: number, height: number, coating: boolean) {
    this.used.add(card.id);
    // World grid coordinates keep samples reusable when the particle field's
    // extent changes without moving this card or its clipped sampling region.
    const signature = [card.centreX, card.centreY, card.width, card.height, card.radius,
      originX, originY, width, height, coating].join(',');
    let cached = this.cards.get(card.id);
    if (cached?.signature !== signature) {
      cached = { signature, values: new Float64Array(width * height).fill(NaN) };
      this.cards.set(card.id, cached);
    }
    return cached.values;
  }
  end() {
    for (const id of this.cards.keys()) if (!this.used.has(id)) this.cards.delete(id);
  }
}
