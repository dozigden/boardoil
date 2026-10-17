import { onBeforeUnmount } from 'vue';
import type { BoardAnimationHandle, BoardAnimationRunner } from '../utils/boardAnimationRunner';

type BoxProperty = 'height' | 'marginBottom' | 'paddingTop' | 'paddingBottom' | 'borderTopWidth' | 'borderBottomWidth';
type BoxStyles = Partial<Record<BoxProperty, string>>;
export type CardCreationEffect = {
  element: HTMLElement;
  from: BoxStyles;
  to: BoxStyles;
  removing: boolean;
  done: () => void;
};

// Prepare geometry without starting effects; the board coordinates their layout.
export function useCardCreationAnimation(runner: BoardAnimationRunner) {
  const animations = new Map<HTMLElement, { effect: CardCreationEffect; animation: BoardAnimationHandle }>();
  const replacements = new Set<HTMLElement>();

  function play(effect: CardCreationEffect) {
    const { element, from, to, done } = effect;
    animations.get(element)?.animation.cancel();
    const animation = runner.animate(element, [
      { ...from, overflow: 'clip', minHeight: '0' },
      { ...to, overflow: 'clip', minHeight: '0' }
    ], {
      onFinish() {
        animations.delete(element);
        element.style.removeProperty('height');
        element.style.removeProperty('margin-bottom');
        done();
      },
      onCancel: () => { animations.delete(element); }
    });
    if (animation.active) {
      animations.set(element, { effect, animation });
    }
  }

  function capture(columnIds: Set<number>) {
    const effects: CardCreationEffect[] = [];
    for (const { effect } of animations.values()) {
      const column = effect.element.closest<HTMLElement>('[data-column-id]');
      if (column && columnIds.has(Number(column.dataset.columnId))) {
        effects.push({ ...effect, from: readStyles(effect.element, effect.from) });
      }
    }
    return effects;
  }

  function suspend(effects: CardCreationEffect[]) {
    for (const effect of effects) {
      animations.get(effect.element)?.animation.cancel();
    }
  }

  function retarget(effect: CardCreationEffect, element: HTMLElement) {
    let to = effect.to;
    if (!effect.removing) {
      to = readStyles(element, effect.to);
    }
    return { ...effect, element, to };
  }

  function withFinalLayout<T>(effects: CardCreationEffect[], measure: () => T): T {
    const original = effects.map(effect => ({ element: effect.element, styles: readInlineStyles(effect.element, effect.to) }));
    try {
      for (const effect of effects) {
        Object.assign(effect.element.style, effect.to);
      }
      return measure();
    } finally {
      for (const { element, styles } of original) {
        Object.assign(element.style, styles);
      }
    }
  }

  function prepareInsert(card: HTMLElement, followingCardMarginBefore: number): CardCreationEffect {
    const style = getComputedStyle(card);
    const followingCard = card.nextElementSibling;
    let followingCardMargin = 0;
    if (followingCard?.matches('.card')) {
      followingCardMargin = Number.parseFloat(getComputedStyle(followingCard).marginTop);
    }
    // Include slick-boundary spacing so the insertion starts without a jump.
    const fromMargin = followingCardMarginBefore - followingCardMargin
      - Number.parseFloat(style.marginTop) - gap(card);
    return {
      element: card, removing: false, done() {},
      from: {
        height: '0px', paddingTop: '0px', paddingBottom: '0px',
        borderTopWidth: '0px', borderBottomWidth: '0px', marginBottom: `${fromMargin}px`
      },
      to: {
        height: `${card.getBoundingClientRect().height}px`,
        paddingTop: style.paddingTop, paddingBottom: style.paddingBottom,
        borderTopWidth: style.borderTopWidth, borderBottomWidth: style.borderBottomWidth,
        marginBottom: style.marginBottom
      }
    };
  }

  function gap(element: HTMLElement) {
    return Number.parseFloat(getComputedStyle(element.parentElement!).rowGap) || 0;
  }

  function prepareEnter(element: Element, done: () => void): CardCreationEffect {
    const slot = element as HTMLElement;
    const fromHeight = slot.style.height ? slot.getBoundingClientRect().height : 0;
    const fromMargin = slot.style.marginBottom ? Number.parseFloat(slot.style.marginBottom) : -gap(slot);
    slot.style.removeProperty('height');
    slot.style.removeProperty('margin-bottom');
    return {
      element: slot, removing: false, done,
      from: { height: `${fromHeight}px`, marginBottom: `${fromMargin}px` },
      to: { height: `${slot.getBoundingClientRect().height}px`, marginBottom: '0px' }
    };
  }

  function prepareLeave(element: Element, done: () => void): CardCreationEffect | null {
    const slot = element as HTMLElement;
    if (replacements.delete(slot)) {
      done();
      return null;
    }
    return {
      element: slot, removing: true, done,
      from: { height: `${slot.getBoundingClientRect().height}px`, marginBottom: getComputedStyle(slot).marginBottom },
      to: { height: '0px', marginBottom: `${-gap(slot)}px` }
    };
  }

  function preserve(effect: CardCreationEffect) {
    Object.assign(effect.element.style, { height: effect.from.height, marginBottom: effect.from.marginBottom });
  }

  function cancel(element: Element) {
    const slot = element as HTMLElement;
    const active = animations.get(slot);
    if (active) {
      preserve({ ...active.effect, from: readStyles(slot, active.effect.from) });
      active.animation.cancel();
    }
  }

  function prepareReplacement(slot: HTMLElement) {
    replacements.add(slot);
    return slot.getBoundingClientRect().height;
  }

  function prepareReplace(card: HTMLElement, height: number): CardCreationEffect {
    return {
      element: card, removing: false, done() {},
      from: { height: `${height}px`, marginBottom: '0px' },
      to: { height: `${card.getBoundingClientRect().height}px`, marginBottom: '0px' }
    };
  }

  function finish(element: HTMLElement) {
    animations.get(element)?.animation.finish();
  }

  onBeforeUnmount(() => {
    for (const { animation } of animations.values()) {
      animation.cancel();
    }
    animations.clear();
    replacements.clear();
  });

  return {
    capture, suspend, retarget, withFinalLayout, play,
    prepareInsert, prepareEnter, prepareLeave, prepareReplacement, prepareReplace,
    cancel, preserve, finish
  };
}

function readStyles(element: HTMLElement, template: BoxStyles): BoxStyles {
  const style = getComputedStyle(element);
  const result: BoxStyles = {};
  for (const property of Object.keys(template) as BoxProperty[]) {
    result[property] = style[property];
  }
  return result;
}

function readInlineStyles(element: HTMLElement, template: BoxStyles): BoxStyles {
  const result: BoxStyles = {};
  for (const property of Object.keys(template) as BoxProperty[]) {
    result[property] = element.style[property];
  }
  return result;
}
