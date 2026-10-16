import { nextTick, ref, type ComputedRef } from 'vue';
import { useCardStore } from '../stores/cardStore';
import type { AppError } from '../../shared/types/appError';
import type { CardEditModel } from '../../shared/types/boardTypes';
import type { BoardCreationMotion } from './useBoardMotion';

type InlineCardCreateDraft = Pick<CardEditModel, 'boardColumnId' | 'title' | 'cardTypeId'>;

export function useBoardInlineCreation(defaultCardTypeId: ComputedRef<number | null>, motion: BoardCreationMotion) {
  const cardStore = useCardStore();
  const newCardDrafts = ref<Record<number, InlineCardCreateDraft>>({});
  const newCardDraftInputs = ref<Record<number, HTMLInputElement | HTMLTextAreaElement | null>>({});
  const newCardDraftErrors = ref<Record<number, string>>({});
  const savingColumns = new Set<number>();

  async function openNewCardDraft(columnId: number, cardTypeId: number | null = defaultCardTypeId.value) {
    if (newCardDrafts.value[columnId] !== undefined) {
      newCardDrafts.value[columnId]!.cardTypeId = cardTypeId;
      delete newCardDraftErrors.value[columnId];
      newCardDraftInputs.value[columnId]?.focus();
      void motion.draftLayoutChanged();
      return;
    }

    newCardDrafts.value[columnId] = { boardColumnId: columnId, title: '', cardTypeId };
    delete newCardDraftErrors.value[columnId];
    await nextTick();
    newCardDraftInputs.value[columnId]?.focus();
    void motion.draftLayoutChanged();
  }

  function openDefaultCardDraft(columnId: number) {
    void openNewCardDraft(columnId, defaultCardTypeId.value);
  }

  function updateNewCardDraftTitle(columnId: number, value: string) {
    if (newCardDrafts.value[columnId] === undefined) {
      return;
    }
    motion.draftEdited(columnId);
    newCardDrafts.value[columnId]!.title = value;
    delete newCardDraftErrors.value[columnId];
  }

  function closeNewCardDraft(columnId: number) {
    delete newCardDrafts.value[columnId];
    delete newCardDraftInputs.value[columnId];
    delete newCardDraftErrors.value[columnId];
    void motion.draftLayoutChanged();
  }

  function setNewCardDraftInput(columnId: number, element: unknown) {
    newCardDraftInputs.value[columnId] = element instanceof HTMLInputElement || element instanceof HTMLTextAreaElement
      ? element
      : null;
  }

  async function saveNewCardDraft(columnId: number) {
    const draft = newCardDrafts.value[columnId];
    if (!draft || !draft.title.trim() || savingColumns.has(columnId)) {
      return;
    }
    if (draft.cardTypeId === null) {
      draft.cardTypeId = defaultCardTypeId.value;
    }

    savingColumns.add(columnId);
    const creation = motion.beginCreation(columnId);
    try {
      const result = await cardStore.createCard({
        ...draft,
        description: '',
        externalUrl: null,
        tagNames: [],
        assignedUserId: null,
        slickName: null
      }, { suppressValidationFeedback: true });
      if (newCardDrafts.value[columnId] !== draft) {
        return;
      }
      if (!result || result.ok) {
        await creation.complete(result?.ok ? result.data.id : null, () => closeNewCardDraft(columnId));
        return;
      }
      newCardDraftErrors.value[columnId] = resolveCreateCardErrorMessage(result.error);
      newCardDraftInputs.value[columnId]?.focus();
      void motion.draftLayoutChanged();
    } finally {
      creation.dispose();
      savingColumns.delete(columnId);
    }
  }

  return {
    newCardDrafts,
    newCardDraftErrors,
    openNewCardDraft,
    openDefaultCardDraft,
    updateNewCardDraftTitle,
    closeNewCardDraft,
    setNewCardDraftInput,
    saveNewCardDraft
  };
}

function resolveCreateCardErrorMessage(error: AppError) {
  const validationErrors = error.validationErrors ?? {};
  const titleErrors = validationErrors.title ?? validationErrors[''] ?? [];
  if (titleErrors.length > 0) {
    return titleErrors[0]!;
  }
  return error.message;
}
