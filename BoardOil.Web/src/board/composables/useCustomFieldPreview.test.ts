import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { effectScope, nextTick, ref } from 'vue';
import { useCustomFieldPreview } from './useCustomFieldPreview';
import { err, ok } from '../../shared/types/result';
import type { CustomFieldDefinitionInput, CustomFieldInput, CustomFieldPreview } from '../../shared/types/customFieldTypes';

const requestPreview = vi.hoisted(() => vi.fn());
vi.mock('../stores/customFieldStore', () => ({ useCustomFieldStore: () => ({ preview: requestPreview }) }));
const field: CustomFieldDefinitionInput = { dataType: 'integer', code: 'Points', name: 'Estimate', emoji: null,
  nullable: true, showOnCard: true, defaultValue: 0, min: null, max: 5 };
const result: CustomFieldPreview = { affectedCardCount: 2, removedValueCount: 0, replacedValueCount: 2 };
const proposal: CustomFieldInput = { definition: field, appliesTo: { mode: 'all' } };
const empty = { affectedCardCount: 0, removedValueCount: 0, replacedValueCount: 0 };
const scopes: ReturnType<typeof effectScope>[] = [];
function setup() {
  const request = ref<{ boardId: number; customFieldId: number; field: CustomFieldInput } | null>(null);
  const scope = effectScope();
  scopes.push(scope);
  const preview = scope.run(() => useCustomFieldPreview(() => request.value))!;
  return { request, preview, scope };
}

describe('definition previews', () => {
  beforeEach(() => { vi.useFakeTimers(); requestPreview.mockReset(); });
  afterEach(() => { scopes.splice(0).forEach(scope => scope.stop()); vi.useRealTimers(); });

  it('debounces edits and skips unchanged or new-type drafts', async () => {
    const { request, preview } = setup();
    await vi.advanceTimersByTimeAsync(500);
    expect(requestPreview).not.toHaveBeenCalled();
    requestPreview.mockResolvedValue(ok(result));
    request.value = { boardId: 1, customFieldId: 41, field: proposal };
    await nextTick();
    await vi.advanceTimersByTimeAsync(200);
    request.value.field = { ...proposal, appliesTo: { mode: 'selected', cardTypeIds: [] } };
    await nextTick();
    await vi.advanceTimersByTimeAsync(299);
    expect(requestPreview).not.toHaveBeenCalled();
    await vi.advanceTimersByTimeAsync(1);
    expect(requestPreview).toHaveBeenCalledExactlyOnceWith(1, 41, request.value.field);
    expect(preview.impact.value).toEqual(result);
  });

  it('ignores an older draft response after a newer preview has completed', async () => {
    const { request, preview } = setup();
    let finishOld!: (value: ReturnType<typeof ok<CustomFieldPreview>>) => void;
    requestPreview.mockReturnValueOnce(new Promise(resolve => { finishOld = resolve; }));
    request.value = { boardId: 1, customFieldId: 41, field: proposal };
    await nextTick();
    await vi.advanceTimersByTimeAsync(300);
    requestPreview.mockResolvedValueOnce(ok(empty));
    request.value = { boardId: 1, customFieldId: 41, field: { ...proposal, appliesTo: { mode: 'selected', cardTypeIds: [] } } };
    await nextTick();
    await vi.advanceTimersByTimeAsync(300);
    finishOld(ok(result));
    await nextTick();
    expect(preview.impact.value).toEqual(empty);
  });

  it('clears losses and exposes backend validation for invalid settings', async () => {
    const { request, preview } = setup();
    requestPreview.mockResolvedValueOnce(ok(result));
    request.value = { boardId: 1, customFieldId: 41, field: proposal };
    await nextTick();
    await vi.advanceTimersByTimeAsync(300);
    const error = { kind: 'api' as const, message: 'Validation failed.', validationErrors: { 'definition.max': ['Whole number required'] } };
    requestPreview.mockResolvedValueOnce(err(error));
    request.value.field = { ...proposal, definition: { ...field, max: 'invalid' } as CustomFieldDefinitionInput };
    await nextTick();
    expect(preview.impact.value).toBeNull();
    await vi.advanceTimersByTimeAsync(300);
    expect(preview.error.value).toEqual(error);
    request.value = null;
    await nextTick();
    expect(preview.error.value).toBeNull();
  });

  it('cancels queued requests when the editor is closed', async () => {
    const { request, scope } = setup();
    request.value = { boardId: 1, customFieldId: 41, field: proposal };
    await nextTick();
    scope.stop();
    await vi.advanceTimersByTimeAsync(300);
    expect(requestPreview).not.toHaveBeenCalled();
  });
});
