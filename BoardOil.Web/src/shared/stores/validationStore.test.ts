import { beforeEach, describe, expect, it } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useValidationStore } from './validationStore';
import { useUiFeedbackStore } from './uiFeedbackStore';
import { err, ok } from '../types/result';

describe('validationStore', () => {
  beforeEach(() => setActivePinia(createPinia()));

  it('replaces the entire previous result and clears it on success', () => {
    const store = useValidationStore();
    store.setValidationResult(err({ kind: 'api', message: 'Invalid', validationErrors: { name: ['Duplicate'], emoji: ['Invalid emoji'] } }));
    store.setValidationResult(err({ kind: 'network', message: 'Offline' }));
    expect(store.getFieldErrors('name')).toEqual([]);
    expect(store.getFieldErrors('emoji')).toEqual([]);
    expect(store.getValidationMessages()).toEqual(['Offline']);

    store.setValidationResult(ok(undefined));
    expect(store.getValidationMessages()).toEqual([]);
  });

  it('replaces validation globally while categories only filter its display', () => {
    const store = useValidationStore();
    store.setValidationResult(err({ kind: 'api', message: 'Default failure' }));
    store.setValidationResult(err({ kind: 'api', message: 'Invalid comment', validationErrors: { text: ['Comment required'] } }), 'comment');

    expect(store.getValidationMessages()).toEqual(['Comment required']);
    expect(store.getValidationMessages([], 'comment')).toEqual(['Comment required']);
    expect(store.getValidationMessages([], 'editor')).toEqual([]);
    expect(store.getFieldErrors('text')).toEqual(['Comment required']);
    expect(store.getFieldErrors('text', 'comment')).toEqual(['Comment required']);
    expect(store.getFieldErrors('text', 'editor')).toEqual([]);
    store.clearFieldErrors('text', 'editor');
    expect(store.getValidationMessages()).toEqual(['Comment required']);

    store.setValidationResult(err({ kind: 'network', message: 'Editor unavailable' }), 'editor');
    expect(store.getValidationMessages()).toEqual(['Editor unavailable']);
    expect(store.getValidationMessages([], 'comment')).toEqual([]);
    expect(store.getFieldErrors('text')).toEqual([]);

    store.setValidationResult(ok(undefined), 'comment');
    expect(store.getValidationMessages()).toEqual([]);
    expect(store.getValidationMessages([], 'editor')).toEqual([]);
  });

  it('uses the same replacement and correction behaviour for local and API validation', () => {
    const store = useValidationStore();
    store.setValidationResult(err({ kind: 'network', message: 'Offline' }));
    store.setValidationResult(err({ message: 'Validation failed.', validationErrors: { confirmPassword: ['Passwords do not match.'] } }));
    expect(store.getValidationMessages()).toEqual(['Passwords do not match.']);
    store.clearFieldErrors('confirmPassword');
    expect(store.getValidationMessages()).toEqual([]);
    store.setValidationResult(err({ kind: 'api', message: 'Username already exists.' }));
    expect(store.getValidationMessages()).toEqual(['Username already exists.']);
  });

  it('keeps validation separate from page feedback and clears it explicitly', () => {
    const store = useValidationStore();
    const feedback = useUiFeedbackStore();
    feedback.setError('Page error', 'load');
    store.setValidationResult(err({ kind: 'api', message: 'Submission failure' }), 'editor');
    expect(feedback.errorMessage).toBe('Page error');
    feedback.clearError('load');

    expect(store.getValidationMessages()).toEqual(['Submission failure']);
    store.clearValidation();
    expect(store.getValidationMessages()).toEqual([]);
    expect(store.getValidationMessages([], 'editor')).toEqual([]);
  });

  it('keeps unrendered and whole-form validation in the summary', () => {
    const store = useValidationStore();
    store.setValidationResult(err({ kind: 'api', message: 'Validation failed.', validationErrors: {
      name: ['Duplicate'], futureField: ['Additional validation'], '': ['Cannot save this combination']
    } }));

    expect(store.getFieldErrors('name')).toEqual(['Duplicate']);
    expect(store.getValidationMessages(['name'])).toEqual(['Additional validation', 'Cannot save this combination']);
    expect(store.getValidationMessages()).toEqual(['Duplicate', 'Additional validation', 'Cannot save this combination']);
  });

  it('clears only a corrected field without mutating the received result', () => {
    const store = useValidationStore();
    const error = { kind: 'api' as const, message: 'Validation failed.', validationErrors: {
      name: ['Duplicate'], emoji: ['Invalid emoji']
    } };
    store.setValidationResult(err(error), 'editor');
    store.clearFieldErrors('name', 'editor');

    expect(store.getFieldErrors('name', 'editor')).toEqual([]);
    expect(store.getValidationMessages(['name'], 'editor')).toEqual(['Invalid emoji']);
    expect(store.getValidationMessages(['name'], 'comment')).toEqual([]);
    expect(error.validationErrors.name).toEqual(['Duplicate']);

    store.clearFieldErrors('emoji', 'editor');
    expect(store.getValidationMessages([], 'editor')).toEqual([]);
  });

  it('keeps general failures until retry or explicit reset, including empty validation collections', () => {
    const store = useValidationStore();
    store.setValidationResult(err({ kind: 'api', message: 'Unable to save', validationErrors: { name: [] } }));
    store.clearFieldErrors('name');
    store.clearFieldErrors('unrelated');
    expect(store.getValidationMessages(['name'])).toEqual(['Unable to save']);
    store.clearValidation();
    expect(store.getValidationMessages()).toEqual([]);
  });
});
