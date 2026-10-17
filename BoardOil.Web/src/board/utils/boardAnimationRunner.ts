export type BoardAnimationHandle = {
  readonly active: boolean;
  finish: () => void;
  cancel: () => void;
};

type AnimationCallbacks = {
  onFinish: () => void;
  onCancel: () => void;
};

const timing = { duration: 200, easing: 'cubic-bezier(0.2, 0, 0, 1)' };
const inactiveHandle: BoardAnimationHandle = { active: false, finish() {}, cancel() {} };

// One board owns this runner; effects retain their geometry and DOM cleanup.
export function createBoardAnimationRunner(refreshLayout: () => void) {
  const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
  const active = new Set<BoardAnimationHandle>();
  let frame: number | null = null;
  let disposed = false;

  function requestRefresh() {
    if (disposed || frame !== null) {
      return;
    }
    frame = requestAnimationFrame(() => {
      frame = null;
      refreshLayout();
      if (active.size > 0) {
        requestRefresh();
      }
    });
  }

  function animate(element: HTMLElement, keyframes: Keyframe[], callbacks: AnimationCallbacks): BoardAnimationHandle {
    if (disposed) {
      callbacks.onCancel();
      return inactiveHandle;
    }
    if (reducedMotion.matches) {
      callbacks.onFinish();
      requestRefresh();
      return inactiveHandle;
    }

    const animation = element.animate(keyframes, timing);
    let running = true;
    function settle(finished: boolean) {
      if (!running) {
        return;
      }
      running = false;
      active.delete(handle);
      animation.onfinish = null;
      animation.oncancel = null;
      animation.cancel();
      try {
        if (finished) {
          callbacks.onFinish();
        } else {
          callbacks.onCancel();
        }
      } finally {
        requestRefresh();
      }
    }
    const handle: BoardAnimationHandle = {
      get active() { return running; },
      finish: () => settle(true),
      cancel: () => settle(false)
    };
    animation.onfinish = handle.finish;
    animation.oncancel = handle.cancel;
    active.add(handle);
    requestRefresh();
    return handle;
  }

  function finishForReducedMotion() {
    if (reducedMotion.matches) {
      for (const animation of [...active]) {
        animation.finish();
      }
    }
  }

  function dispose() {
    if (disposed) {
      return;
    }
    disposed = true;
    reducedMotion.removeEventListener('change', finishForReducedMotion);
    if (frame !== null) {
      cancelAnimationFrame(frame);
      frame = null;
    }
    for (const animation of [...active]) {
      animation.cancel();
    }
  }

  reducedMotion.addEventListener('change', finishForReducedMotion);
  return {
    get reducedMotion() { return reducedMotion.matches; },
    animate,
    dispose
  };
}

export type BoardAnimationRunner = ReturnType<typeof createBoardAnimationRunner>;
