import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useUiFeedbackStore } from './uiFeedbackStore';

describe('uiFeedbackStore errors', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  it('keeps an error until its source clears it', () => {
    const store = useUiFeedbackStore();
    store.setError('Member lookup unavailable.', 'members');

    store.clearError('comments');
    expect(store.errorMessage).toBe('Member lookup unavailable.');

    store.clearError('members');
    expect(store.errorMessage).toBe('');
    expect(store.errorSource).toBeNull();
  });

  it('does not let an earlier source clear a replacement error with identical text', () => {
    const store = useUiFeedbackStore();
    store.setError('Unavailable.', 'members');
    store.setError('Unavailable.', 'comments');

    store.clearError('members');
    expect(store.errorMessage).toBe('Unavailable.');

    store.clearError('comments');
    expect(store.errorMessage).toBe('');
  });

  it('resets the error and its ownership when the application context is cleared', () => {
    const store = useUiFeedbackStore();
    store.setError('Unavailable.', 'members');

    store.resetError();

    expect(store.errorMessage).toBe('');
    expect(store.errorSource).toBeNull();
    store.setError('New error.', 'comments');
    store.clearError('members');
    expect(store.errorMessage).toBe('New error.');
  });
});

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
