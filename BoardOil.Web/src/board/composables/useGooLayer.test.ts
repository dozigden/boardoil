import { afterEach, describe, expect, it, vi } from 'vitest';
import { computed, effectScope } from 'vue';
import { syncObservedBoardCardElements, useGooLayer } from './useGooLayer';
import { columnGooBottomExtensions } from '../utils/gooViewport';

const cleanup = vi.hoisted(() => [] as (() => void)[]);
vi.mock('vue', async importOriginal => ({
  ...await importOriginal<typeof import('vue')>(),
  onMounted: vi.fn(),
  onBeforeUnmount: (callback: () => void) => cleanup.push(callback)
}));

afterEach(() => {
  for (const callback of cleanup.splice(0).reverse()) { callback(); }
  vi.unstubAllGlobals();
});

describe('column goo viewport', () => {
  it('opens and restores the production bottom clip without shifting cached card geometry', () => {
    const { layer, clips, cards, scrollTo } = makeScrollableLayer();
    scrollTo(70);
    const blob = () => layer.gooGroups.value[0]!.blobs.find(blob => blob.id === 'card-6')!;
    const original = blob();
    expect(original.clipInsets!.bottom).toBeGreaterThan(0);
    const measurements = cards.map(card => card.getBoundingClientRect.mock.calls.length);
    columnGooBottomExtensions.set(clips[0] as unknown as HTMLElement, 200);
    scrollTo(70);
    expect(blob().clipInsets?.bottom ?? 0).toBe(0);
    expect(blob()).toMatchObject({ left: original.left, top: original.top, width: original.width, height: original.height });
    expect(cards.map(card => card.getBoundingClientRect.mock.calls.length)).toEqual(measurements);
    columnGooBottomExtensions.set(clips[0] as unknown as HTMLElement, 0);
    scrollTo(70);
    expect(blob().clipInsets).toEqual(original.clipInsets);
  });
  it('bounds a tall slick to cards near the viewport while retaining an offscreen buffer', () => {
    const { layer } = makeScrollableLayer();
    const blobs = layer.gooGroupsByColumnId.value.get(1)![0]!.blobs;
    expect(blobs.map(blob => blob.id)).toContain('card-1');
    expect(blobs.map(blob => blob.id)).toContain('card-7');
    expect(blobs.map(blob => blob.id)).not.toContain('card-80');
    expect(blobs.length).toBeLessThan(12);
  });

  it('refreshes column blobs from cached geometry when scrolling to the bottom and back', () => {
    const { layer, scrollTo, cards } = makeScrollableLayer();
    const initialMeasurements = cards.map(card => card.getBoundingClientRect.mock.calls.length);
    scrollTo(7500);
    const bottomBlobs = layer.gooGroupsByColumnId.value.get(1)![0]!.blobs;
    expect(bottomBlobs.map(blob => blob.id)).toContain('card-80');
    expect(bottomBlobs.map(blob => blob.id)).not.toContain('card-1');
    expect(bottomBlobs.length).toBeLessThan(12);
    scrollTo(0);
    expect(layer.gooGroupsByColumnId.value.get(1)![0]!.blobs.map(blob => blob.id)).toContain('card-1');
    expect(cards.map(card => card.getBoundingClientRect.mock.calls.length)).toEqual(initialMeasurements);
    scrollTo(10000);
    expect(layer.gooGroupsByColumnId.value.size).toBe(0);
  });

  it('measures each column once per refresh without remeasuring cards on scroll', () => {
    const { layer, clips, cards, scrollTo } = makeScrollableLayer([{}, { left: 340 }]);
    const initialMeasurements = cards.map(card => card.getBoundingClientRect.mock.calls.length);
    expect(clips.map(clip => clip.getBoundingClientRect.mock.calls.length)).toEqual([1, 1]);

    scrollTo(7500);

    expect(clips.map(clip => clip.getBoundingClientRect.mock.calls.length)).toEqual([2, 2]);
    expect(cards.map(card => card.getBoundingClientRect.mock.calls.length)).toEqual(initialMeasurements);
    expect(layer.gooGroupsByColumnId.value.get(1)![0]!.blobs.map(blob => blob.id)).toContain('card-80');
    expect(layer.gooGroupsByColumnId.value.get(2)![0]!.blobs.map(blob => blob.id)).toContain('card-81');

    layer.refreshGooLayout();

    expect(clips.map(clip => clip.getBoundingClientRect.mock.calls.length)).toEqual([3, 3]);
  });

  it('preserves local and board coordinates with padding, scrollbar gutters and independent scrolling', () => {
    const { layer, clips, board, scrollTo } = makeScrollableLayer([
      { paddingLeft: 12, paddingTop: 10, clientLeft: 6 },
      { left: 340, paddingLeft: 18, paddingTop: 20, clientLeft: 9 }
    ]);

    scrollTo(300);
    scrollTo(100, 2);
    clips.forEach(clip => { clip.left -= 120; });
    board.scrollLeft = 120;
    scrollTo(300);

    const first = layer.gooGroupsByColumnId.value.get(1)![0]!.blobs.find(blob => blob.id === 'card-4')!;
    const second = layer.gooGroupsByColumnId.value.get(2)![0]!.blobs.find(blob => blob.id === 'card-82')!;
    expect(first).toMatchObject({ left: 29, top: 350 });
    expect(second).toMatchObject({ left: 35, top: 160 });
    const boardBlobs = layer.gooGroups.value[0]!.blobs;
    expect(boardBlobs.find(blob => blob.id === 'card-4')).toMatchObject({ left: 35, top: 150 });
    expect(boardBlobs.find(blob => blob.id === 'card-82')).toMatchObject({ left: 384, top: 160 });
  });
});

describe('syncObservedBoardCardElements', () => {
  it('observes every supplied card element without observing existing targets twice', () => {
    const firstCard = makeElement();
    const secondCard = makeElement();
    const observer = makeObserver();
    const observedElements = new Set<HTMLElement>();

    syncObservedBoardCardElements(observer, observedElements, [firstCard, secondCard]);
    syncObservedBoardCardElements(observer, observedElements, [firstCard, secondCard]);

    expect(observer.observe).toHaveBeenCalledTimes(2);
    expect(observer.observe).toHaveBeenCalledWith(firstCard);
    expect(observer.observe).toHaveBeenCalledWith(secondCard);
    expect(observer.unobserve).not.toHaveBeenCalled();
    expect(observedElements).toEqual(new Set([firstCard, secondCard]));
  });

  it('unobserves card elements that are no longer supplied', () => {
    const removedCard = makeElement();
    const retainedCard = makeElement();
    const observer = makeObserver();
    const observedElements = new Set([removedCard, retainedCard]);

    syncObservedBoardCardElements(observer, observedElements, [retainedCard]);

    expect(observer.unobserve).toHaveBeenCalledOnce();
    expect(observer.unobserve).toHaveBeenCalledWith(removedCard);
    expect(observer.observe).not.toHaveBeenCalled();
    expect(observedElements).toEqual(new Set([retainedCard]));
  });
});

function makeElement() {
  return {} as HTMLElement;
}

function makeObserver(): Pick<ResizeObserver, 'observe' | 'unobserve'> {
  return {
    observe: vi.fn(),
    unobserve: vi.fn()
  };
}

type TestColumnLayout = {
  left?: number;
  paddingLeft?: number;
  paddingTop?: number;
  clientLeft?: number;
};

function makeScrollableLayer(columnLayouts: TestColumnLayout[] = [{}]) {
  class Element {
    isConnected = true;
  }
  let pendingFrame: FrameRequestCallback | null = null;
  let onScroll = () => {};
  vi.stubGlobal('HTMLElement', Element);
  vi.stubGlobal('window', { location: { search: '' }, removeEventListener: vi.fn() });
  vi.stubGlobal('getComputedStyle', (element: { paddingLeft: number; paddingTop: number }) => ({
    paddingLeft: String(element.paddingLeft), paddingRight: '0', paddingTop: String(element.paddingTop), paddingBottom: '0'
  }));
  vi.stubGlobal('requestAnimationFrame', (callback: FrameRequestCallback) => {
    pendingFrame = callback;
    return 1;
  });
  vi.stubGlobal('cancelAnimationFrame', () => { pendingFrame = null; });
  const clips = columnLayouts.map(layout => {
    const clip = Object.assign(new Element(), {
      left: layout.left ?? 0,
      paddingLeft: layout.paddingLeft ?? 0,
      paddingTop: layout.paddingTop ?? 0,
      scrollTop: 0, scrollLeft: 0, clientLeft: layout.clientLeft ?? 0,
      getBoundingClientRect: vi.fn(() => ({ left: clip.left, top: 100, width: 300, height: 500 }))
    });
    return clip;
  });
  const cards = Array.from({ length: 80 * clips.length }, (_, index) => {
    const clip = clips[Math.floor(index / 80)]!;
    return Object.assign(new Element(), {
      dataset: { cardId: String(index + 1) },
      closest: () => clip,
      getBoundingClientRect: vi.fn(() => ({
        left: clip.left + clip.clientLeft + clip.paddingLeft + 20 - clip.scrollLeft,
        top: 100 + clip.paddingTop + (index % 80) * 100 - clip.scrollTop,
        width: 250, height: 80
      }))
    });
  });
  const board = Object.assign(new Element(), {
    scrollTop: 0, scrollLeft: 0,
    getBoundingClientRect: () => ({ left: 0, top: 0, width: 1000, height: 600 }),
    querySelectorAll: () => cards,
    addEventListener: (_name: string, callback: () => void) => { onScroll = callback; },
    removeEventListener: vi.fn()
  });
  const scope = effectScope();
  cleanup.push(() => scope.stop());
  const layer = scope.run(() => useGooLayer(
    computed(() => cards.map((_, index) => ({
      cardId: index + 1, columnId: Math.floor(index / 80) + 1, itemId: `card-${index + 1}`, groupKey: 'slick-1', colour: '#03a9f4'
    }))),
    computed(() => 'membership'),
    computed(() => 'style'),
    new Map()
  ))!;
  layer.setBoardRef(board);
  const flushFrame = () => {
    const frame = pendingFrame;
    pendingFrame = null;
    frame?.(0);
  };
  flushFrame();
  return {
    layer,
    cards,
    clips,
    board,
    scrollTo(top: number, columnId = 1) {
      clips[columnId - 1]!.scrollTop = top;
      onScroll();
      flushFrame();
    }
  };
}
