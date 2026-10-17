import { afterEach, describe, expect, it, vi } from 'vitest';
import { createBoardAnimationRunner, type BoardAnimationRunner } from './boardAnimationRunner';

const runners: BoardAnimationRunner[] = [];
afterEach(() => {
  for (const runner of runners.splice(0)) {
    runner.dispose();
  }
  vi.unstubAllGlobals();
});

describe('board animation runner', () => {
  it('coalesces overlapping height/movement effects and their natural completion into one refresh per frame', () => {
    const browser = makeBrowser();
    const refresh = vi.fn();
    const runner = makeRunner(refresh);
    const callbacks = Array.from({ length: 40 }, (_, index) => {
      const finished = vi.fn();
      const frames = index % 2 === 0 ? [{ height: '0px' }, { height: '50px' }] : [{ transform: 'translateY(50px)' }, { transform: 'none' }];
      runner.animate(browser.element, frames, { onFinish: finished, onCancel: vi.fn() });
      return finished;
    });

    expect(browser.frames.size).toBe(1);
    expect(refresh).not.toHaveBeenCalled();
    browser.flushFrame();
    expect(refresh).toHaveBeenCalledTimes(1);
    expect(browser.frames.size).toBe(1);

    for (const animation of browser.animations) {
      animation.onfinish?.();
      expect(animation.cancel).toHaveBeenCalledOnce();
    }
    expect(callbacks.every(callback => callback.mock.calls.length === 1)).toBe(true);
    expect(refresh).toHaveBeenCalledTimes(1);
    expect(browser.frames.size).toBe(1);
    browser.flushFrame();
    expect(refresh).toHaveBeenCalledTimes(2);
    expect(browser.frames.size).toBe(0);
    browser.flushFrame();
    expect(refresh).toHaveBeenCalledTimes(2);
  });

  it('cancels only the requested effect and ignores its previously queued completion', () => {
    const browser = makeBrowser();
    const runner = makeRunner(vi.fn());
    const onFinish = vi.fn();
    const onCancel = vi.fn();
    const first = runner.animate(browser.element, [], { onFinish, onCancel });
    const queuedCompletion = browser.animations[0]!.onfinish!;
    const second = runner.animate(browser.element, [], { onFinish: vi.fn(), onCancel: vi.fn() });

    first.cancel();
    first.cancel();
    queuedCompletion();
    first.finish();
    expect(onCancel).toHaveBeenCalledOnce();
    expect(onFinish).not.toHaveBeenCalled();
    expect(browser.animations[0]!.cancel).toHaveBeenCalledOnce();
    expect(first.active).toBe(false);
    expect(second.active).toBe(true);
    browser.flushFrame();
    expect(browser.frames.size).toBe(1);
    second.finish();
    browser.flushFrame();
    expect(browser.frames.size).toBe(0);
  });

  it('runs cancellation cleanup when the browser cancels an animation', () => {
    const browser = makeBrowser();
    const runner = makeRunner(vi.fn());
    const onCancel = vi.fn();
    const onFinish = vi.fn();
    const handle = runner.animate(browser.element, [], { onFinish, onCancel });
    browser.animations[0]!.oncancel!();
    handle.cancel();
    expect(onCancel).toHaveBeenCalledOnce();
    expect(onFinish).not.toHaveBeenCalled();
    expect(handle.active).toBe(false);
  });

  it('applies reduced-motion effects immediately without creating native animations', () => {
    const browser = makeBrowser(true);
    const refresh = vi.fn();
    const runner = makeRunner(refresh);
    const onFinish = vi.fn();
    const handle = runner.animate(browser.element, [], { onFinish, onCancel: vi.fn() });
    expect(runner.reducedMotion).toBe(true);
    expect(onFinish).toHaveBeenCalledOnce();
    expect(browser.element.animate).not.toHaveBeenCalled();
    expect(handle.active).toBe(false);
    handle.finish();
    handle.cancel();
    expect(onFinish).toHaveBeenCalledOnce();
    browser.flushFrame();
    expect(refresh).toHaveBeenCalledOnce();
    expect(browser.frames.size).toBe(0);
  });

  it('finishes all effects once when reduced motion is enabled and skips effects created by completion callbacks', () => {
    const browser = makeBrowser();
    const refresh = vi.fn();
    const runner = makeRunner(refresh);
    const nestedFinish = vi.fn();
    const creationFinish = vi.fn(() => {
      runner.animate(browser.element, [], { onFinish: nestedFinish, onCancel: vi.fn() });
    });
    const movementFinish = vi.fn();
    const cancel = vi.fn();
    runner.animate(browser.element, [{ height: '0px' }], { onFinish: creationFinish, onCancel: cancel });
    runner.animate(browser.element, [{ transform: 'none' }], { onFinish: movementFinish, onCancel: cancel });
    browser.setReducedMotion(false);
    expect(creationFinish).not.toHaveBeenCalled();
    browser.setReducedMotion(true);
    browser.setReducedMotion(true);

    expect(creationFinish).toHaveBeenCalledOnce();
    expect(movementFinish).toHaveBeenCalledOnce();
    expect(nestedFinish).toHaveBeenCalledOnce();
    expect(browser.animations).toHaveLength(2);
    expect(cancel).not.toHaveBeenCalled();
    browser.flushFrame();
    expect(refresh).toHaveBeenCalledOnce();
    expect(browser.frames.size).toBe(0);
  });

  it('keeps refreshing when completion starts a replacement animation', () => {
    const browser = makeBrowser();
    const refresh = vi.fn();
    const runner = makeRunner(refresh);
    const replacementFinish = vi.fn();
    const first = runner.animate(browser.element, [], {
      onFinish: () => { runner.animate(browser.element, [], { onFinish: replacementFinish, onCancel: vi.fn() }); },
      onCancel: vi.fn()
    });
    browser.flushFrame();
    first.finish();
    browser.flushFrame();
    expect(refresh).toHaveBeenCalledTimes(2);
    expect(browser.frames.size).toBe(1);
    browser.animations[1]!.onfinish!();
    browser.flushFrame();
    expect(replacementFinish).toHaveBeenCalledOnce();
    expect(browser.frames.size).toBe(0);
  });

  it('disposes effects and the preference listener once, without refreshing a removed board', () => {
    const browser = makeBrowser();
    const refresh = vi.fn();
    const runner = makeRunner(refresh);
    const onFinish = vi.fn();
    const onCancel = vi.fn();
    const handle = runner.animate(browser.element, [], { onFinish, onCancel });
    const queuedCompletion = browser.animations[0]!.onfinish!;
    runner.dispose();
    runner.dispose();
    queuedCompletion();
    handle.finish();
    browser.flushFrame();
    browser.setReducedMotion(true);
    expect(onCancel).toHaveBeenCalledOnce();
    expect(onFinish).not.toHaveBeenCalled();
    expect(browser.animations[0]!.cancel).toHaveBeenCalledOnce();
    expect(browser.listeners.size).toBe(0);
    expect(refresh).not.toHaveBeenCalled();
    const lateCancel = vi.fn();
    runner.animate(browser.element, [], { onFinish, onCancel: lateCancel });
    expect(lateCancel).toHaveBeenCalledOnce();
    expect(browser.animations).toHaveLength(1);
    expect(browser.frames.size).toBe(0);
  });
});

function makeRunner(refresh: () => void) {
  const runner = createBoardAnimationRunner(refresh);
  runners.push(runner);
  return runner;
}

function makeBrowser(initialReducedMotion = false) {
  const frames = new Map<number, FrameRequestCallback>();
  const listeners = new Set<() => void>();
  let nextFrameId = 0;
  const media = {
    matches: initialReducedMotion,
    addEventListener: (_event: string, callback: () => void) => { listeners.add(callback); },
    removeEventListener: (_event: string, callback: () => void) => { listeners.delete(callback); }
  };
  vi.stubGlobal('window', { matchMedia: () => media });
  vi.stubGlobal('requestAnimationFrame', (callback: FrameRequestCallback) => {
    frames.set(++nextFrameId, callback);
    return nextFrameId;
  });
  vi.stubGlobal('cancelAnimationFrame', (id: number) => { frames.delete(id); });
  const animations: { onfinish: (() => void) | null; oncancel: (() => void) | null; cancel: ReturnType<typeof vi.fn> }[] = [];
  const element = { animate: vi.fn(() => {
    const animation = { onfinish: null, oncancel: null, cancel: vi.fn() };
    animations.push(animation);
    return animation as unknown as Animation;
  }) } as unknown as HTMLElement;
  return {
    element,
    animations,
    frames,
    listeners,
    setReducedMotion(value: boolean) {
      media.matches = value;
      for (const listener of listeners) { listener(); }
    },
    flushFrame() {
      const current = [...frames.values()];
      frames.clear();
      for (const callback of current) { callback(16); }
    }
  };
}
