import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useUiFeedbackStore } from './uiFeedbackStore';

describe('uiFeedbackStore toasts', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('shows and automatically clears a transient toast', () => {
    const store = useUiFeedbackStore();

    store.showToast('Copied');

    expect(store.toastMessage).toBe('Copied');
    expect(store.toastTone).toBe('success');

    vi.advanceTimersByTime(3000);

    expect(store.toastMessage).toBe('');
  });

  it('restarts the timeout when a newer toast replaces the current one', () => {
    const store = useUiFeedbackStore();
    store.showToast('First message.');
    vi.advanceTimersByTime(2000);

    store.showToast('Copy failed.', 'error');
    vi.advanceTimersByTime(1000);

    expect(store.toastMessage).toBe('Copy failed.');
    expect(store.toastTone).toBe('error');

    vi.advanceTimersByTime(2000);

    expect(store.toastMessage).toBe('');
  });
});
