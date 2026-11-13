import { afterEach, describe, expect, it, vi } from 'vitest';
import type { BoardColumn } from '../../shared/types/boardTypes';
import { boardGooClips } from '../utils/gooViewport';
import { BoardGooGeometry } from './boardGeometry';

afterEach(() => vi.unstubAllGlobals());

function fixture() {
  const onResize = vi.fn();
  const observer = { observe: vi.fn(), unobserve: vi.fn(), disconnect: vi.fn() };
  let resized = () => {};
  vi.stubGlobal('ResizeObserver', class {
    constructor(callback: () => void) { resized = callback; }
    observe = observer.observe;
    unobserve = observer.unobserve;
    disconnect = observer.disconnect;
  });
  vi.stubGlobal('getComputedStyle', () => ({ borderTopLeftRadius: '12px' }));
  const board = {
    clientLeft: 2, clientTop: 3, clientWidth: 900, clientHeight: 600, scrollLeft: 11, scrollTop: 0,
    getBoundingClientRect: vi.fn(() => ({ left: 40, top: 30, width: 904, height: 606 })),
    querySelectorAll: (selector: string) => selector === '.column-content' ? clips : elements
  };
  const originX = () => 40 + board.clientLeft - board.scrollLeft;
  const originY = () => 30 + board.clientTop - board.scrollTop;
  const clips = [0, 1].map(index => ({
    dataset: { columnId: String(index + 10) }, clientLeft: 1, clientTop: 2, scrollLeft: 0, scrollTop: 0,
    getBoundingClientRect: vi.fn(() => ({ left: originX() + 20 + index * 340, top: originY() + 70,
      width: 300, height: 500 - index * 240, bottom: originY() + 570 - index * 240 }))
  }));
  const elements = [0, 1, 2].map(index => {
    const column = index % 2, clip = clips[column];
    return {
      dataset: { cardId: String(index + 1) }, isConnected: true, width: 260,
      getBoundingClientRect: vi.fn(() => ({ left: originX() + 20 + column * 340 + clip.clientLeft + 15 - clip.scrollLeft,
        top: originY() + 70 + clip.clientTop + 20 + index * 110 - clip.scrollTop,
        width: elements[index].width, height: 90 }))
    };
  });
  const floating = { isConnected: true,
    getBoundingClientRect: vi.fn(() => ({ left: originX() + 260, top: originY() + 90, width: 260, height: 90 })) };
  const floatingCards = new Map<number, HTMLElement>();
  const columns = clips.map((_, index) => ({ id: index + 10,
    cards: elements.filter((_, i) => i % 2 === index).map(element => ({ id: Number(element.dataset.cardId), slickId: 1 }))
  })) as BoardColumn[];
  const boardElement = board as unknown as HTMLElement;
  const geometry = new BoardGooGeometry(boardElement, onResize);
  const measure = () => geometry.measure(columns, floatingCards);
  const reads = () => [board, ...clips, ...elements, floating].map(el => el.getBoundingClientRect.mock.calls.length);
  return { board, boardElement, clips, elements, floating, floatingCards, columns, geometry, measure, reads,
    observer, onResize, resize: () => resized() };
}

describe('experimental board geometry', () => {
  it('projects independent column scrolling without DOM reads and matches remeasurement', () => {
    const f = fixture();
    f.measure();
    const local = f.geometry.localColumns(f.floatingCards);
    for (const [index, left, top] of [[0, 7, 95], [1, 0, 60], [0, 0, 0]]) {
      const before = f.reads();
      f.clips[index].scrollLeft = left; f.clips[index].scrollTop = top;
      const target = f.clips[index] as unknown as HTMLElement;
      expect(f.geometry.projectScroll(target, f.floatingCards)).toBe(true);
      expect(f.reads()).toEqual(before);
      const projected = structuredClone(f.geometry.cards);
      expect(f.measure().cards).toEqual(projected);
      expect(f.geometry.localColumns(f.floatingCards)).toEqual(local);
      expect(f.geometry.projectScroll(target, f.floatingCards)).toBe(false);
    }
    f.geometry.dispose();
  });

  it('projects board scrolling without changing card coordinates or measuring elements', () => {
    const f = fixture();
    f.measure();
    const cards = f.geometry.cards, before = f.reads();
    f.board.scrollLeft = 180;
    expect(f.geometry.projectScroll(f.boardElement, f.floatingCards)).toBe(true);
    expect(f.geometry.viewport?.left).toBe(180);
    expect(f.geometry.cards).toBe(cards);
    expect(f.reads()).toEqual(before);
    expect(f.measure().cards).toEqual(cards);
    expect(f.geometry.projectScroll(f.boardElement, f.floatingCards)).toBe(false);
    expect(f.geometry.projectScroll(null, f.floatingCards)).toBe(false);
    f.geometry.dispose();
  });

  it('tracks floating overrides, excludes them from native outlines and resumes tracking on landing', () => {
    const f = fixture();
    f.measure();
    f.floatingCards.set(1, f.floating as unknown as HTMLElement);
    f.measure();
    const travelling = f.geometry.cards.find(card => card.id === 1)!;
    expect(travelling).toMatchObject({ centreX: 390, centreY: 135 });
    expect(f.observer.unobserve).toHaveBeenCalledWith(f.elements[0]);
    expect(f.observer.observe).toHaveBeenCalledWith(f.floating);
    expect(f.geometry.localColumns(f.floatingCards).flatMap(c => c.cards).some(c => c.id === 1)).toBe(false);
    f.clips[0].scrollTop = 120;
    f.geometry.projectScroll(f.clips[0] as unknown as HTMLElement, f.floatingCards);
    expect(f.geometry.cards.find(card => card.id === 1)).toBe(travelling);
    const projected = structuredClone(f.geometry.cards);
    expect(f.measure().cards).toEqual(projected);
    f.floatingCards.clear();
    f.measure();
    expect(f.observer.unobserve).toHaveBeenCalledWith(f.floating);
    expect(f.geometry.cards.find(card => card.id === 1)?.centreY).toBe(17);
    expect(f.geometry.localColumns(f.floatingCards)[0].cards.some(c => c.id === 1)).toBe(true);
    f.geometry.dispose();
  });

  it('uses shared clipping, refreshes observation targets and disconnects on disposal', () => {
    const f = fixture();
    expect(f.measure().viewport).toMatchObject({ clipTop: 70, clipBottom: 570 });
    boardGooClips.set(f.boardElement, { top: 80, bottom: 600 });
    expect(f.measure().viewport).toMatchObject({ clipTop: 80, clipBottom: 600 });
    f.elements[0].isConnected = false;
    f.elements[1].width = 0;
    expect(f.measure().cards.map(c => c.id)).toEqual([3]);
    expect(f.observer.unobserve).toHaveBeenCalledWith(f.elements[0]);
    f.resize();
    expect(f.onResize).toHaveBeenCalledOnce();
    f.geometry.dispose();
    expect(f.observer.disconnect).toHaveBeenCalledOnce();
    expect(f.geometry.cards).toEqual([]);
    expect(f.geometry.viewport).toBeNull();
    expect(f.geometry.localColumns(f.floatingCards)).toEqual([]);
  });
});
