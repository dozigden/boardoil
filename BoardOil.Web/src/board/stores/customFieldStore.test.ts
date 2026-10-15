import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useCustomFieldStore } from './customFieldStore';
import { computed } from 'vue';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import { ok, err, type Result } from '../../shared/types/result';
import type { CustomField } from '../../shared/types/customFieldTypes';
import type { AppError } from '../../shared/types/appError';
const api = { getCustomFields: vi.fn(), createCustomField: vi.fn(), updateCustomField: vi.fn(), deleteCustomField: vi.fn(), reorderCustomFields: vi.fn() };
vi.mock('../../shared/api/boardApi', () => ({ createBoardApi: () => api }));
const field: CustomField = { id: 41, definition: { dataType: 'integer', code: 'Points', name: 'Estimate', emoji: null,
  nullable: false, defaultValue: 0, showOnCard: true, min: null, max: null }, appliesTo: { mode: 'all' } };
const other: CustomField = { id: 42, definition: { ...field.definition, code: 'Other', name: 'Other' }, appliesTo: { mode: 'selected', cardTypeIds: [2] } };
function deferred<T>() { let resolve!: (value: T) => void; const promise = new Promise<T>(done => { resolve = done; }); return { promise, resolve }; }
beforeEach(() => { setActivePinia(createPinia()); vi.resetAllMocks(); api.getCustomFields.mockResolvedValue(ok([field, other])); });
describe('custom field catalogue', () => {
  it('resolves all and selected fields in catalogue order and leaves unused definitions in the catalogue', async () => {
    const store = useCustomFieldStore();
    api.getCustomFields.mockResolvedValueOnce(ok([field, other, { ...other, id: 43, appliesTo: { mode: 'selected', cardTypeIds: [] } }]));
    await store.loadCustomFields(1);
    expect(store.resolve(99).map(field => field.code)).toEqual(['Points']);
    expect(store.resolve(2).map(field => field.code)).toEqual(['Points', 'Other']);
    expect(store.fields).toHaveLength(3);
  });
  it('updates resolved definitions reactively and copies response data', async () => {
    const store = useCustomFieldStore();
    await store.loadCustomFields(1);
    const definitions = computed(() => store.resolve(2));
    expect(definitions.value.map(field => field.code)).toEqual(['Points', 'Other']);
    const response = [{ ...field, definition: { ...field.definition, name: 'Renamed' } }, other];
    api.updateCustomField.mockResolvedValue(ok(response));
    await store.save(1, field.id, response[0]);
    expect(api.updateCustomField).toHaveBeenCalledWith(1, field.id, response[0]);
    expect(store.fields[0].id).toBe(field.id);
    response[0].definition.name = 'External mutation';
    expect(definitions.value[0].name).toBe('Renamed');
    expect(api.getCustomFields).toHaveBeenCalledTimes(1);
  });
  it.each(['board change', 'disposal'])('ignores pending loads after %s', async change => {
    const store = useCustomFieldStore();
    const pending = deferred<Result<CustomField[], AppError>>();
    api.getCustomFields.mockReturnValueOnce(pending.promise);
    const load = store.loadCustomFields(1);
    if (change === 'board change') await store.loadCustomFields(2);
    else store.dispose();
    useUiFeedbackStore().setError('Current error', 'customField');
    pending.resolve(err({ kind: 'api', message: 'Stale error' }));
    expect(await load).toBe(false);
    expect(useUiFeedbackStore().errorMessage).toBe('Current error');
    expect(store.activeBoardId).toBe(change === 'board change' ? 2 : null);
  });
  it('ignores a write response after disposal and leaves the new board busy state intact', async () => {
    const store = useCustomFieldStore();
    await store.loadCustomFields(1);
    const pending = deferred<Result<CustomField[], AppError>>();
    api.updateCustomField.mockReturnValueOnce(pending.promise);
    const saving = store.save(1, field.id, field);
    store.dispose();
    await store.loadCustomFields(2);
    store.busy = true;
    pending.resolve(ok([]));
    await saving;
    expect(store.fields).toHaveLength(2);
    expect(store.busy).toBe(true);
  });
  it('retains the confirmed order on failure and adopts the canonical response on success', async () => {
    const store = useCustomFieldStore();
    await store.loadCustomFields(1);
    api.reorderCustomFields.mockResolvedValueOnce(err({ kind: 'api', message: 'Try again' }));
    expect((await store.reorder(1, [other.id, field.id])).ok).toBe(false);
    expect(store.fields.map(field => field.definition.code)).toEqual(['Points', 'Other']);
    api.reorderCustomFields.mockResolvedValueOnce(ok([other, field]));
    await store.reorder(1, [other.id, field.id]);
    expect(api.reorderCustomFields).toHaveBeenLastCalledWith(1, [other.id, field.id]);
    expect(store.fields.map(field => field.definition.code)).toEqual(['Other', 'Points']);
    expect(store.busy).toBe(false);
  });
});
