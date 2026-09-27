import { describe, expect, it, vi } from 'vitest';
import type { SystemJobsRealtime } from './systemJobsRealtime';
import { createSystemJobsContext } from './systemJobsContext';

function deferred() {
  let resolve!: () => void;
  let reject!: (error: Error) => void;
  const promise = new Promise<void>((done, fail) => { resolve = done; reject = fail; });
  return { promise, resolve, reject };
}

describe('system jobs context', () => {
  it('suspends callbacks immediately while a pending connection stops after leaving or logout', async () => {
    const pending = deferred();
    let callbacksActive = false;
    const changed = vi.fn();
    const realtime: SystemJobsRealtime = {
      connect: vi.fn(() => { callbacksActive = true; return pending.promise; }),
      suspend: vi.fn(() => { callbacksActive = false; }),
      disconnect: vi.fn(async () => { callbacksActive = false; })
    };
    const dispose = vi.fn();
    const warning = vi.fn();
    const context = createSystemJobsContext(realtime, dispose, warning);

    const entered = context.setActive(true);
    await Promise.resolve();
    const left = context.setActive(false);
    if (callbacksActive) changed();
    expect(realtime.suspend).toHaveBeenCalledTimes(1);
    expect(dispose).toHaveBeenCalledTimes(1);
    expect(changed).not.toHaveBeenCalled();
    pending.resolve();
    await Promise.all([entered, left]);
    expect(realtime.disconnect).toHaveBeenCalledTimes(1);
    expect(warning).not.toHaveBeenCalled();
  });

  it('skips a queued connection when logout happens before it starts', async () => {
    const realtime: SystemJobsRealtime = {
      connect: vi.fn(async () => undefined),
      suspend: vi.fn(),
      disconnect: vi.fn(async () => undefined)
    };
    const dispose = vi.fn();
    const warning = vi.fn();
    const context = createSystemJobsContext(realtime, dispose, warning);

    const entered = context.setActive(true);
    const left = context.setActive(false);
    await Promise.all([entered, left]);

    expect(realtime.connect).not.toHaveBeenCalled();
    expect(realtime.disconnect).toHaveBeenCalledTimes(1);
    expect(dispose).toHaveBeenCalledTimes(1);
    expect(warning).not.toHaveBeenCalled();
  });

  it('does not surface a late connection failure after leaving', async () => {
    const pending = deferred();
    const realtime: SystemJobsRealtime = {
      connect: vi.fn(() => pending.promise),
      suspend: vi.fn(),
      disconnect: vi.fn(async () => undefined)
    };
    const warning = vi.fn();
    const context = createSystemJobsContext(realtime, vi.fn(), warning);

    const entered = context.setActive(true);
    await Promise.resolve();
    const left = context.setActive(false);
    pending.reject(new Error('connection failed'));
    await Promise.all([entered, left]);

    expect(realtime.disconnect).toHaveBeenCalledTimes(1);
    expect(warning).not.toHaveBeenCalled();
  });
});
