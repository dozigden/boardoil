import { ref, watch } from 'vue';
import type { AppError } from '../../shared/types/appError';
import type { CustomFieldDefinitionInput, CardTypeFieldImpact } from '../../shared/types/customFieldTypes';
import { useCardTypeStore } from '../stores/cardTypeStore';

type PreviewRequest = { boardId: number; cardTypeId: number; customFields: CustomFieldDefinitionInput[] };

export function useCardTypeFieldsPreview(request: () => PreviewRequest | null) {
  const store = useCardTypeStore();
  const impacts = ref<CardTypeFieldImpact[]>([]);
  const error = ref<AppError | null>(null);

  watch(request, (current, _previous, onCleanup) => {
    impacts.value = [];
    error.value = null;
    if (!current) return;

    // A response describes this draft only; later edits or closing the editor invalidate it.
    let cancelled = false;
    const timer = setTimeout(async () => {
      const result = await store.previewFields(current.boardId, current.cardTypeId, current.customFields);
      if (cancelled) return;
      if (result.ok) impacts.value = result.data.customFields;
      else error.value = result.error;
    }, 300);
    onCleanup(() => {
      cancelled = true;
      clearTimeout(timer);
    });
  }, { immediate: true, deep: true });

  return { impacts, error };
}
