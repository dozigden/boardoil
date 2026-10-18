import { onBeforeUnmount } from 'vue';
import type { BoardAnimationHandle, BoardAnimationRunner } from '../utils/boardAnimationRunner';

type BoxProperty = 'height' | 'marginTop' | 'marginBottom' | 'opacity' | 'boxShadow' | 'paddingTop' | 'paddingBottom' | 'borderTopWidth' | 'borderBottomWidth';
type BoxStyles = Partial<Record<BoxProperty, string>>;
export type CardLayoutEffect = {
  element: HTMLElement;
  from: BoxStyles;
  to: BoxStyles;
  removing: boolean;
  done: () => void;
};

// Creation, editor handoff and removal share box effects; the board coordinates their layout.
export function useCardLayoutAnimation(runner: BoardAnimationRunner) {
  const animations = new Map<HTMLElement, { effect: CardLayoutEffect; animation: BoardAnimationHandle }>();
  const replacements = new Set<HTMLElement>();

  function play(effect: CardLayoutEffect) {
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
    const effects: CardLayoutEffect[] = [];
    for (const { effect } of animations.values()) {
      const column = effect.element.closest<HTMLElement>('[data-column-id]');
      if (column && columnIds.has(Number(column.dataset.columnId))) {
        effects.push({ ...effect, from: readStyles(effect.element, effect.from) });
      }
    }
    return effects;
  }

  function suspend(effects: CardLayoutEffect[]) {
    for (const effect of effects) {
      animations.get(effect.element)?.animation.cancel();
    }
  }

  function retarget(effect: CardLayoutEffect, element: HTMLElement) {
    let to = effect.to;
    if (!effect.removing) {
      to = readStyles(element, effect.to);
    }
    return { ...effect, element, to };
  }

  function withFinalLayout<T>(effects: CardLayoutEffect[], measure: () => T): T {
    const original = effects.map(effect => ({ element: effect.element, styles: readInlineStyles(effect.element, effect.to) }));
    const ancestors = new Set<HTMLElement>();
    const scrollPositions = new Map<HTMLElement, { top: number; left: number }>();
    for (const { element } of effects) {
      for (let ancestor = element.parentElement; ancestor && !ancestors.has(ancestor); ancestor = ancestor.parentElement) {
        ancestors.add(ancestor);
        if (ancestor.scrollTop !== 0 || ancestor.scrollLeft !== 0) {
          scrollPositions.set(ancestor, { top: ancestor.scrollTop, left: ancestor.scrollLeft });
        }
      }
    }
    try {
      for (const effect of effects) {
        Object.assign(effect.element.style, effect.to);
      }
      return measure();
    } finally {
      for (const { element, styles } of original) {
        Object.assign(element.style, styles);
      }
      // Measuring collapsed endpoints can clamp a scrolled column. Restore its
      // offset after restoring its content, before any starting boxes are read.
      for (const [element, { top, left }] of scrollPositions) {
        element.scrollTop = top;
        element.scrollLeft = left;
      }
    }
  }

  function prepareInsert(card: HTMLElement, followingCardMarginBefore: number): CardLayoutEffect {
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

  function prepareRemoval(card: HTMLElement, cancelGap: boolean, done: () => void, captured?: CardLayoutEffect): CardLayoutEffect {
    const style = getComputedStyle(card);
    const from = {
      height: `${card.getBoundingClientRect().height}px`,
      marginTop: style.marginTop, marginBottom: style.marginBottom, opacity: style.opacity,
      paddingTop: style.paddingTop, paddingBottom: style.paddingBottom,
      borderTopWidth: style.borderTopWidth, borderBottomWidth: style.borderBottomWidth,
      ...captured?.from
    };
    // Browsers round positive border widths up to a pixel, which otherwise
    // leaves a 2px snap at zero height. Paint the horizontal borders as inset
    // shadows during removal and fold their space into the shrinking padding.
    const shadows = [`inset 0 1px 0 ${style.borderTopColor}`, `inset 0 -1px 0 ${style.borderBottomColor}`];
    if (style.boxShadow !== 'none') { shadows.push(style.boxShadow); }
    const boxShadow = shadows.join(', ');
    from.paddingTop = `${Number.parseFloat(from.paddingTop) + Number.parseFloat(from.borderTopWidth)}px`;
    from.paddingBottom = `${Number.parseFloat(from.paddingBottom) + Number.parseFloat(from.borderBottomWidth)}px`;
    from.borderTopWidth = '0px';
    from.borderBottomWidth = '0px';
    return {
      element: card, removing: true, done,
      from: { ...from, boxShadow },
      to: {
        height: '0px', marginTop: '0px', marginBottom: `${cancelGap ? -gap(card) : 0}px`, opacity: '0',
        paddingTop: '0px', paddingBottom: '0px', borderTopWidth: '0px', borderBottomWidth: '0px', boxShadow
      }
    };
  }

  function gap(element: HTMLElement) {
    return Number.parseFloat(getComputedStyle(element.parentElement!).rowGap) || 0;
  }

  function prepareEnter(element: Element, done: () => void): CardLayoutEffect {
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

  function prepareLeave(element: Element, done: () => void): CardLayoutEffect | null {
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

  function preserve(effect: CardLayoutEffect) {
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

  function prepareReplace(card: HTMLElement, height: number): CardLayoutEffect {
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
    prepareInsert, prepareRemoval, prepareEnter, prepareLeave, prepareReplacement, prepareReplace,
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
