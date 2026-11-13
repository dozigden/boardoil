import type { BoardColumn } from '../../shared/types/boardTypes';
import { boardGooClips } from '../utils/gooViewport';
import type { GooCard, Viewport } from './types';

export class BoardGooGeometry {
  cards: GooCard[] = [];
  viewport: Viewport | null = null;
  private originX = 0;
  private originY = 0;
  private columnScrolls = new Map<HTMLElement, { column: number; left: number; top: number }>();
  private observed = new Set<Element>();
  private resizeObserver: ResizeObserver;

  constructor(private board: HTMLElement, onResize: () => void) {
    this.resizeObserver = new ResizeObserver(onResize);
  }

  measure(columns: BoardColumn[], floatingCards: ReadonlyMap<number, HTMLElement>) {
    const board = this.board;
    const boardRect = board.getBoundingClientRect();
    const left = board.scrollLeft, top = board.scrollTop;
    const originX = boardRect.left + board.clientLeft - left;
    const originY = boardRect.top + board.clientTop - top;
    const width = board.clientWidth, height = board.clientHeight;
    const elements = new Map<number, HTMLElement>();
    board.querySelectorAll<HTMLElement>('.column-content > .card[data-card-id]').forEach(el => elements.set(Number(el.dataset.cardId), el));
    floatingCards.forEach((el, id) => elements.set(id, el));
    const cards: GooCard[] = [];
    const nextObserved = new Set<Element>([board]);
    columns.forEach((column, index) => {
      for (const card of column.cards) {
        const element = elements.get(card.id);
        if (!element?.isConnected) continue;
        nextObserved.add(element);
        const rect = element.getBoundingClientRect();
        if (rect.width <= 0 || rect.height <= 0) continue;
        const radius = Number.parseFloat(getComputedStyle(element).borderTopLeftRadius) || 0;
        cards.push({ id: card.id, group: card.slickId ?? null, column: index,
          centreX: rect.left - originX + rect.width / 2, centreY: rect.top - originY + rect.height / 2,
          width: rect.width, height: rect.height, radius });
      }
    });
    let clipTop = top, clipBottom = top + height;
    const clips = [...board.querySelectorAll<HTMLElement>('.column-content')];
    this.columnScrolls.clear();
    for (const clip of clips) {
      const column = columns.findIndex(c => c.id === Number(clip.dataset.columnId));
      if (column >= 0) this.columnScrolls.set(clip, { column, left: clip.scrollLeft, top: clip.scrollTop });
    }
    if (clips.length) {
      const rects = clips.map(el => { nextObserved.add(el); return el.getBoundingClientRect(); });
      clipTop = Math.max(top, Math.min(...rects.map(r => r.top - originY)));
      clipBottom = Math.min(top + height, Math.max(...rects.map(r => r.bottom - originY)));
    }
    const sharedClip = boardGooClips.get(board);
    if (sharedClip) { clipTop = top + sharedClip.top; clipBottom = top + sharedClip.bottom; }
    for (const element of this.observed) if (!nextObserved.has(element)) { this.resizeObserver.unobserve(element); this.observed.delete(element); }
    for (const element of nextObserved) if (!this.observed.has(element)) { this.resizeObserver.observe(element); this.observed.add(element); }
    this.cards = cards;
    this.viewport = { left, top, width, height, clipTop, clipBottom };
    this.originX = originX; this.originY = originY;
    return { cards, viewport: this.viewport };
  }

  projectScroll(target: EventTarget | null, floatingCards: ReadonlyMap<number, HTMLElement>): boolean {
    if (!this.viewport) return false;
    const board = this.board;
    if (target === board) {
      if (this.viewport.left === board.scrollLeft && this.viewport.top === board.scrollTop) return false;
      this.viewport = { ...this.viewport, left: board.scrollLeft, top: board.scrollTop };
    } else {
      const scroller = target as HTMLElement;
      const previous = this.columnScrolls.get(scroller);
      if (!previous) return false;
      const dx = previous.left - scroller.scrollLeft, dy = previous.top - scroller.scrollTop;
      if (!dx && !dy) return false;
      this.cards = this.cards.map(card => card.column === previous.column && !floatingCards.has(card.id)
        ? { ...card, centreX: card.centreX + dx, centreY: card.centreY + dy } : card);
      previous.left = scroller.scrollLeft; previous.top = scroller.scrollTop;
    }
    return true;
  }

  localColumns(floatingCards: ReadonlyMap<number, HTMLElement>) {
    return [...this.columnScrolls].map(([element, scroll]) => {
      const rect = element.getBoundingClientRect();
      const x = rect.left + element.clientLeft - this.originX - element.scrollLeft;
      const y = rect.top + element.clientTop - this.originY - element.scrollTop;
      const local = this.cards.filter(card => card.column === scroll.column && !floatingCards.has(card.id))
        .map(card => ({ ...card, centreX: card.centreX - x, centreY: card.centreY - y }));
      return { index: scroll.column, element, cards: local };
    });
  }

  dispose() {
    this.resizeObserver.disconnect();
    this.observed.clear();
    this.columnScrolls.clear();
    this.cards = [];
    this.viewport = null;
  }
}
