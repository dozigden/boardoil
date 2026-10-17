import type { Page } from '@playwright/test';

// Freeze real browser animations so geometry can be checked between endpoints,
// without sleeps or a dependency on how quickly the test runner clicks.
export async function controlLayoutAnimations(page: Page) {
  await page.addInitScript(() => {
    const animate = Element.prototype.animate;
    Element.prototype.animate = function (keyframes, options) {
      const animation = animate.call(this, keyframes, options);
      if (animation.effect instanceof KeyframeEffect
        && animation.effect.getKeyframes().some(frame => frame.height !== undefined || frame.transform !== undefined)) {
        animation.pause();
      }
      return animation;
    };
  });
}

export async function seekLayoutAnimations(page: Page, progress: number) {
  return page.evaluate(fraction => {
    let count = 0;
    for (const animation of document.getAnimations()) {
      if (animation instanceof CSSAnimation || animation instanceof CSSTransition
        || !(animation.effect instanceof KeyframeEffect)
        || !animation.effect.getKeyframes().some(frame => frame.height !== undefined || frame.transform !== undefined)) {
        continue;
      }
      count++;
      if (fraction === 1) {
        animation.finish();
      } else {
        animation.currentTime = Number(animation.effect.getTiming().duration) * fraction;
      }
    }
    return count;
  }, progress);
}

