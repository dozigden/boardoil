import { ref } from 'vue';

export type EntityEditDraftOptions<TSource, TDraft, TId> = {
  getId: (source: TSource) => TId;
  toDraft: (source: TSource) => TDraft;
  cloneDraft: (draft: TDraft) => TDraft;
  areEqual: (left: TDraft, right: TDraft) => boolean;
};

export function useEntityEditDraft<TSource, TDraft, TId>(options: EntityEditDraftOptions<TSource, TDraft, TId>) {
  const draft = ref<TDraft | null>(null);
  const sourceId = ref<TId | null>(null);
  const isDirty = ref(false);

  function clear() {
    draft.value = null;
    sourceId.value = null;
    isDirty.value = false;
  }

  function initFromSource(source: TSource) {
    const nextSourceId = options.getId(source);
    if (sourceId.value === nextSourceId) {
      return false;
    }

    const nextDraft = options.cloneDraft(options.toDraft(source));
    draft.value = nextDraft;
    sourceId.value = nextSourceId;
    isDirty.value = false;
    return true;
  }

  function patchFromSystem(update: Partial<TDraft>) {
    if (draft.value === null) {
      return;
    }

    draft.value = {
      ...draft.value,
      ...update
    };
  }

  function patchFromUser(update: Partial<TDraft>) {
    if (draft.value === null) {
      return;
    }

    const previousDraft = draft.value;
    const nextDraft = {
      ...previousDraft,
      ...update
    };
    draft.value = nextDraft;

    if (!isDirty.value && !options.areEqual(nextDraft, previousDraft)) {
      isDirty.value = true;
    }
  }

  return {
    draft,
    sourceId,
    isDirty,
    initFromSource,
    patchFromUser,
    patchFromSystem,
    clear
  };
}
