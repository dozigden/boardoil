import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { effectScope, nextTick, ref } from 'vue';
import { useCardTypeFieldsPreview } from './useCardTypeFieldsPreview';
import { err, ok } from '../../shared/types/result';
import type { CardFieldDefinitionInput, CardTypeFieldsPreview } from '../../shared/types/cardFieldTypes';

const previewFields = vi.hoisted(() => vi.fn());
vi.mock('../stores/cardTypeStore', () => ({ useCardTypeStore: () => ({ previewFields }) }));
const field: CardFieldDefinitionInput = { type: 'integer', fieldCode: 'Points', displayName: 'Estimate', emoji: null,
  nullable: true, showOnCard: true, defaultValue: 0, min: null, max: 5 };
const result: CardTypeFieldsPreview = { fields: [{ fieldCode: 'Points', displayName: 'Estimate', change: 'replaced', cardCount: 2 }] };
const scopes: ReturnType<typeof effectScope>[] = [];
function setup() {
  const request = ref<{ boardId: number; cardTypeId: number; fields: CardFieldDefinitionInput[] } | null>(null);
  const scope = effectScope();
  scopes.push(scope);
  const preview = scope.run(() => useCardTypeFieldsPreview(() => request.value))!;
  return { request, preview, scope };
}

describe('definition previews', () => {
  beforeEach(() => { vi.useFakeTimers(); previewFields.mockReset(); });
  afterEach(() => { scopes.splice(0).forEach(scope => scope.stop()); vi.useRealTimers(); });

  it('debounces edits and skips unchanged or new-type drafts', async () => {
    const { request, preview } = setup();
    await vi.advanceTimersByTimeAsync(500);
    expect(previewFields).not.toHaveBeenCalled();
    previewFields.mockResolvedValue(ok(result));
    request.value = { boardId: 1, cardTypeId: 2, fields: [field] };
    await nextTick();
    await vi.advanceTimersByTimeAsync(200);
    request.value.fields = [];
    await nextTick();
    await vi.advanceTimersByTimeAsync(299);
    expect(previewFields).not.toHaveBeenCalled();
    await vi.advanceTimersByTimeAsync(1);
    expect(previewFields).toHaveBeenCalledExactlyOnceWith(1, 2, []);
    expect(preview.impacts.value).toEqual(result.fields);
  });

  it('ignores an older draft response after a newer preview has completed', async () => {
    const { request, preview } = setup();
    let finishOld!: (value: ReturnType<typeof ok<CardTypeFieldsPreview>>) => void;
    previewFields.mockReturnValueOnce(new Promise(resolve => { finishOld = resolve; }));
    request.value = { boardId: 1, cardTypeId: 2, fields: [field] };
    await nextTick();
    await vi.advanceTimersByTimeAsync(300);
    previewFields.mockResolvedValueOnce(ok({ fields: [] }));
    request.value = { boardId: 1, cardTypeId: 2, fields: [] };
    await nextTick();
    await vi.advanceTimersByTimeAsync(300);
    finishOld(ok(result));
    await nextTick();
    expect(preview.impacts.value).toEqual([]);
  });

  it('clears losses and exposes backend validation for invalid settings', async () => {
    const { request, preview } = setup();
    previewFields.mockResolvedValueOnce(ok(result));
    request.value = { boardId: 1, cardTypeId: 2, fields: [field] };
    await nextTick();
    await vi.advanceTimersByTimeAsync(300);
    const error = { kind: 'api' as const, message: 'Validation failed.', validationErrors: { 'fields[0].max': ['Whole number required'] } };
    previewFields.mockResolvedValueOnce(err(error));
    request.value.fields = [{ ...field, max: 'invalid' } as CardFieldDefinitionInput];
    await nextTick();
    expect(preview.impacts.value).toEqual([]);
    await vi.advanceTimersByTimeAsync(300);
    expect(preview.error.value).toEqual(error);
    request.value = null;
    await nextTick();
    expect(preview.error.value).toBeNull();
  });

  it('cancels queued requests when the editor is closed', async () => {
    const { request, scope } = setup();
    request.value = { boardId: 1, cardTypeId: 2, fields: [field] };
    await nextTick();
    scope.stop();
    await vi.advanceTimersByTimeAsync(300);
    expect(previewFields).not.toHaveBeenCalled();
  });
});
