import { ref, watch } from 'vue';
import type { AppError } from '../../shared/types/appError';
import type { CustomFieldInput, CustomFieldPreview } from '../../shared/types/customFieldTypes';
import { useCustomFieldStore } from '../stores/customFieldStore';

type PreviewRequest = { boardId: number; customFieldId: number; field: CustomFieldInput };
export function useCustomFieldPreview(request: () => PreviewRequest | null) {
  const store = useCustomFieldStore();
  const impact = ref<CustomFieldPreview | null>(null);
  const error = ref<AppError | null>(null);
  const pending = ref(false);
  watch(request, (current, _previous, onCleanup) => {
    impact.value = null;
    error.value = null;
    pending.value = current !== null;
    if (!current) return;
    let cancelled = false;
    const timer = setTimeout(async () => {
      const result = await store.preview(current.boardId, current.customFieldId, current.field);
      if (cancelled) return;
      pending.value = false;
      if (result.ok) impact.value = result.data;
      else error.value = result.error;
    }, 300);
    onCleanup(() => { cancelled = true; clearTimeout(timer); });
  }, { immediate: true, deep: true });
  return { impact, error, pending };
}
