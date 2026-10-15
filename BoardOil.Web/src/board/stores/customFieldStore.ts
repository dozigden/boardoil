import { defineStore } from 'pinia';
import { ref } from 'vue';
import { createBoardApi } from '../../shared/api/boardApi';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import type { CustomField, CustomFieldInput } from '../../shared/types/customFieldTypes';
import type { Result } from '../../shared/types/result';
import type { AppError } from '../../shared/types/appError';

export const useCustomFieldStore = defineStore('customField', () => {
  const fields = ref<CustomField[]>([]);
  const activeBoardId = ref<number | null>(null);
  const busy = ref(false);
  const api = createBoardApi();
  const feedback = useUiFeedbackStore();

  function SET_FIELDS(boardId: number, next: CustomField[]) {
    if (activeBoardId.value !== boardId) return;
    fields.value = next.map(field => ({ id: field.id, definition: { ...field.definition }, appliesTo: copyApplicability(field) }));
  }

  function dispose() {
    activeBoardId.value = null;
    fields.value = [];
    busy.value = false;
  }

  async function loadCustomFields(boardId: number) {
    if (activeBoardId.value !== boardId) dispose();
    activeBoardId.value = boardId;
    const result = await api.getCustomFields(boardId);
    if (activeBoardId.value !== boardId) return false;
    if (!result.ok) {
      feedback.setError(result.error.message, 'customField');
      return false;
    }
    SET_FIELDS(boardId, result.data);
    feedback.clearError('customField');
    return true;
  }

  function resolve(cardTypeId: number | null) {
    return fields.value.filter(field => field.appliesTo.mode === 'all'
      || (cardTypeId !== null && field.appliesTo.cardTypeIds.includes(cardTypeId))).map(field => field.definition);
  }

  async function save(boardId: number, customFieldId: number | null, field: CustomFieldInput) {
    return write(boardId, () => customFieldId === null ? api.createCustomField(boardId, field) : api.updateCustomField(boardId, customFieldId, field));
  }

  function remove(boardId: number, customFieldId: number) {
    return write(boardId, () => api.deleteCustomField(boardId, customFieldId));
  }

  function reorder(boardId: number, ids: number[]) {
    // Keep the confirmed order visible until the server accepts the new order.
    return write(boardId, () => api.reorderCustomFields(boardId, ids));
  }

  function preview(boardId: number, customFieldId: number, field: CustomFieldInput | null) {
    return api.previewCustomField(boardId, customFieldId, field);
  }

  async function write(boardId: number, operation: () => Promise<Result<CustomField[], AppError>>) {
    if (activeBoardId.value === boardId) busy.value = true;
    try {
      const result = await operation();
      if (result.ok) SET_FIELDS(boardId, result.data);
      return result;
    } finally {
      if (activeBoardId.value === boardId) busy.value = false;
    }
  }

  return { fields, activeBoardId, busy, dispose, loadCustomFields, resolve, save, remove, reorder, preview };
});

function copyApplicability(field: CustomField) {
  if (field.appliesTo.mode === 'all') return { mode: 'all' as const };
  return { mode: 'selected' as const, cardTypeIds: [...field.appliesTo.cardTypeIds] };
}
