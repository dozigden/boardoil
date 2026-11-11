import { afterEach, describe, expect, it, vi } from 'vitest';
import { computed, effectScope } from 'vue';
import { syncObservedBoardCardElements, useGooLayer } from './useGooLayer';

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

function makeScrollableLayer() {
  class Element {
    isConnected = true;
  }
  let pendingFrame: FrameRequestCallback | null = null;
  let onScroll = () => {};
  vi.stubGlobal('HTMLElement', Element);
  vi.stubGlobal('window', { location: { search: '' }, removeEventListener: vi.fn() });
  vi.stubGlobal('getComputedStyle', () => ({
    paddingLeft: '0', paddingRight: '0', paddingTop: '0', paddingBottom: '0'
  }));
  vi.stubGlobal('requestAnimationFrame', (callback: FrameRequestCallback) => {
    pendingFrame = callback;
    return 1;
  });
  vi.stubGlobal('cancelAnimationFrame', () => { pendingFrame = null; });
  const clip = Object.assign(new Element(), {
    scrollTop: 0, scrollLeft: 0, clientLeft: 0,
    getBoundingClientRect: () => ({ left: 0, top: 100, width: 300, height: 500 })
  });
  const cards = Array.from({ length: 80 }, (_, index) => Object.assign(new Element(), {
    dataset: { cardId: String(index + 1) },
    closest: () => clip,
    getBoundingClientRect: vi.fn(() => ({
      left: 20, top: 100 + index * 100 - clip.scrollTop, width: 250, height: 80
    }))
  }));
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
      cardId: index + 1, columnId: 1, itemId: `card-${index + 1}`, groupKey: 'slick-1', colour: '#03a9f4'
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
    scrollTo(top: number) {
      clip.scrollTop = top;
      onScroll();
      flushFrame();
    }
  };
}
