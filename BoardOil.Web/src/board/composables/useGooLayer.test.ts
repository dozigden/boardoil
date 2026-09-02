import { describe, expect, it, vi } from 'vitest';
import { syncObservedBoardCardElements } from './useGooLayer';

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
