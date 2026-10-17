import { onBeforeUnmount } from 'vue';
import type { BoardAnimationHandle, BoardAnimationRunner } from '../utils/boardAnimationRunner';

// Animate real layout height so cards and their scroll container move together.
export function useCardCreationAnimation(runner: BoardAnimationRunner) {
  const animations = new Map<HTMLElement, BoardAnimationHandle>();
  const replacements = new Set<HTMLElement>();

  function animate(element: HTMLElement, from: Keyframe, to: Keyframe, done: () => void) {
    animations.get(element)?.cancel();
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
      animations.set(element, animation);
    }
  }

  function animateHeight(
    element: HTMLElement,
    fromHeight: number,
    toHeight: number,
    fromMargin: number,
    toMargin: number,
    done: () => void
  ) {
    animate(element,
      { height: `${fromHeight}px`, marginBottom: `${fromMargin}px` },
      { height: `${toHeight}px`, marginBottom: `${toMargin}px` },
      done);
  }

  function insert(card: HTMLElement, followingCardMarginBefore: number) {
    const style = getComputedStyle(card);
    const followingCard = card.nextElementSibling;
    let followingCardMargin = 0;
    if (followingCard?.matches('.card')) {
      followingCardMargin = Number.parseFloat(getComputedStyle(followingCard).marginTop);
    }
    // A new slick card can also change the following card's boundary spacing.
    const fromMargin = followingCardMarginBefore - followingCardMargin
      - Number.parseFloat(style.marginTop) - gap(card);
    animate(card, {
      height: '0px',
      paddingTop: '0px',
      paddingBottom: '0px',
      borderTopWidth: '0px',
      borderBottomWidth: '0px',
      marginBottom: `${fromMargin}px`
    }, {
      height: `${card.getBoundingClientRect().height}px`,
      paddingTop: style.paddingTop,
      paddingBottom: style.paddingBottom,
      borderTopWidth: style.borderTopWidth,
      borderBottomWidth: style.borderBottomWidth,
      marginBottom: style.marginBottom
    }, () => {});
  }

  function gap(element: HTMLElement) {
    return Number.parseFloat(getComputedStyle(element.parentElement!).rowGap) || 0;
  }

  function enter(element: Element, done: () => void) {
    const slot = element as HTMLElement;
    const fromHeight = slot.style.height ? slot.getBoundingClientRect().height : 0;
    const fromMargin = slot.style.marginBottom ? Number.parseFloat(slot.style.marginBottom) : -gap(slot);
    slot.style.removeProperty('height');
    slot.style.removeProperty('margin-bottom');
    animateHeight(slot, fromHeight, slot.getBoundingClientRect().height, fromMargin, 0, done);
  }

  function leave(element: Element, done: () => void) {
    const slot = element as HTMLElement;
    if (replacements.delete(slot)) {
      done();
      return;
    }
    const margin = Number.parseFloat(getComputedStyle(slot).marginBottom);
    animateHeight(slot, slot.getBoundingClientRect().height, 0, margin, -gap(slot), done);
  }

  function cancel(element: Element) {
    const slot = element as HTMLElement;
    const animation = animations.get(slot);
    if (!animation) {
      return;
    }

    // Preserve the displayed size when an opening animation is interrupted.
    slot.style.height = `${slot.getBoundingClientRect().height}px`;
    slot.style.marginBottom = getComputedStyle(slot).marginBottom;
    animation.cancel();
  }

  function prepareReplacement(slot: HTMLElement) {
    const height = slot.getBoundingClientRect().height;
    replacements.add(slot);
    cancel(slot);
    return height;
  }

  function replace(card: HTMLElement, height: number) {
    animateHeight(card, height, card.getBoundingClientRect().height, 0, 0, () => {});
  }

  function finish(element: HTMLElement) {
    animations.get(element)?.finish();
  }

  onBeforeUnmount(() => {
    for (const animation of animations.values()) {
      animation.cancel();
    }
    animations.clear();
    replacements.clear();
  });

  return { enter, leave, cancel, prepareReplacement, replace, finish, insert };
}
