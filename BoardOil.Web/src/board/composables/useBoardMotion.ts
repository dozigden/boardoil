import { computed, nextTick, onBeforeUnmount, shallowReactive, watch, type ComputedRef, type Ref } from 'vue';
import type { BoardColumn, Slick } from '../../shared/types/boardTypes';
import { useGooLayer } from './useGooLayer';
import { useCardCreationAnimation } from './useCardCreationAnimation';
import { useCardMovementAnimation } from './useCardMovementAnimation';
import { findMovedColumns, type CardIdsByColumn } from '../utils/cardMovement';
import { createBoardAnimationRunner } from '../utils/boardAnimationRunner';
import type { GooRendererMode } from '../utils/gooRendererMode';
import { buildSlickGooDescriptors, buildSlickGooMembershipSignature, buildSlickGooStyleSignature } from '../utils/slickGooAdapter';
import { buildSelectionGooDescriptors, buildSelectionGooMembershipSignature, createSelectionGooStyle } from '../utils/selectionGooAdapter';

export function useBoardMotion(
  columns: ComputedRef<BoardColumn[]>,
  cardIdsByColumnId: ComputedRef<CardIdsByColumn>,
  slicks: Ref<Slick[]>,
  selectedCardIds: Ref<number[]>,
  selectionMode: Ref<boolean>,
  gooRendererMode: GooRendererMode
) {
  let boardElement: HTMLElement | null = null;
  const allCardIds = computed(() => Object.values(cardIdsByColumnId.value).flat());
  const pendingCreations = shallowReactive(new Map<number, Set<number>>());
  // A local create can reach the store through realtime before its HTTP reply.
  // Hold new arrivals in that column until the editor and saved card can be
  // swapped in one render. Existing cards continue receiving updates normally.
  const renderedColumns = computed(() => {
    if (pendingCreations.size === 0) {
      return columns.value;
    }
    return columns.value.map(column => {
      const knownCardIds = pendingCreations.get(column.id);
      if (!knownCardIds) {
        return column;
      }
      return { ...column, cards: column.cards.filter(card => knownCardIds.has(card.id)) };
    });
  });
  const slicksById = computed(() => new Map(slicks.value.map(slick => [slick.id, slick] as const)));
  const selectedCardIdSet = computed(() => new Set(selectedCardIds.value));
  const selectionStyle = createSelectionGooStyle();
  const animationRunner = createBoardAnimationRunner(refreshGooLayout);
  onBeforeUnmount(animationRunner.dispose);
  const movement = useCardMovementAnimation(animationRunner);
  const gooColumns = computed(() => {
    if (gooRendererMode === 'full') {
      return renderedColumns.value;
    }
    return renderedColumns.value.map(column => ({
      ...column, cards: column.cards.filter(card => !movement.floatingCards.has(card.id))
    }));
  });
  const goo = useGooLayer(
    computed(() => buildSlickGooDescriptors(gooColumns.value, slicksById.value)),
    computed(() => buildSlickGooMembershipSignature(gooColumns.value)),
    computed(() => buildSlickGooStyleSignature(slicks.value)),
    movement.floatingCards
  );
  const selectionGoo = useGooLayer(
    computed(() => {
      if (!selectionMode.value) {
        return [];
      }
      return buildSelectionGooDescriptors(gooColumns.value, selectedCardIdSet.value, selectionStyle.colour);
    }),
    computed(() => buildSelectionGooMembershipSignature(gooColumns.value, selectedCardIdSet.value, selectionMode.value)),
    computed(() => selectionStyle.styleSignature),
    movement.floatingCards
  );
  function refreshGooLayout() {
    goo.refreshGooLayout();
    selectionGoo.refreshGooLayout();
  }
  const creationAnimation = useCardCreationAnimation(animationRunner);

  watch(cardIdsByColumnId, async (current, previous) => {
    const movedColumns = findMovedColumns(previous, current);
    if (movedColumns.size === 0) {
      return;
    }
    const positions = movement.capture(movedColumns);
    await nextTick();
    movement.play(positions);
  });

  watch(renderedColumns, async () => {
    await nextTick();
    movement.prune();
  });

  watch(allCardIds, async (cardIds, previousCardIds) => {
    if (!boardElement) {
      return;
    }
    const previousIds = new Set(previousCardIds);
    const newIds = new Set(cardIds.filter(cardId => !previousIds.has(cardId)));
    const arrivingIds = renderedColumns.value.flatMap(column => column.cards
      .filter(card => newIds.has(card.id)).map(card => card.id));
    if (arrivingIds.length === 0) {
      return;
    }
    const previousMargins = new Map<number, number>();
    for (const card of boardElement.querySelectorAll<HTMLElement>('.column-content > .card[data-card-id]')) {
      previousMargins.set(Number(card.dataset.cardId), Number.parseFloat(getComputedStyle(card).marginTop));
    }
    await nextTick();
    for (const cardId of arrivingIds) {
      const card = getCardElement(cardId);
      if (card) {
        const followingId = Number((card.nextElementSibling as HTMLElement | null)?.dataset.cardId);
        creationAnimation.insert(card, previousMargins.get(followingId) ?? 0);
      }
    }
  });

  function setBoardRef(element: unknown) {
    boardElement = element instanceof HTMLElement ? element : null;
    goo.setBoardRef(element);
    selectionGoo.setBoardRef(element);
    movement.setBoardRef(element);
  }

  function getCardElement(cardId: number) {
    return boardElement?.querySelector<HTMLElement>(`.column-content > .card[data-card-id="${cardId}"]`);
  }

  function getDraftSlot(columnId: number) {
    return boardElement?.querySelector<HTMLElement>(`[data-inline-column-id="${columnId}"]`);
  }

  async function draftLayoutChanged() {
    await nextTick();
    goo.scheduleGooStructureRefresh();
    selectionGoo.scheduleGooStructureRefresh();
  }

  function draftEdited(columnId: number) {
    const slot = getDraftSlot(columnId);
    if (slot) {
      creationAnimation.finish(slot);
    }
  }

  function beginCreation(columnId: number) {
    // Include filtered-out cards so changing filters during the save reveals them immediately.
    const knownCardIds = new Set(allCardIds.value);
    pendingCreations.set(columnId, knownCardIds);

    function release() {
      if (pendingCreations.get(columnId) === knownCardIds) {
        pendingCreations.delete(columnId);
      }
    }

    async function complete(cardId: number | null, closeDraft: () => void) {
      await nextTick();
      const currentSlot = getDraftSlot(columnId);
      const cardIsVisible = columns.value.some(column => column.id === columnId
        && column.cards.some(card => card.id === cardId));
      const replacementHeight = currentSlot && cardIsVisible
        ? creationAnimation.prepareReplacement(currentSlot)
        : null;
      release();
      closeDraft();
      if (replacementHeight !== null) {
        await nextTick();
        const card = cardId === null ? null : getCardElement(cardId);
        if (card) {
          creationAnimation.replace(card, replacementHeight);
        }
      }
    }

    return { complete, dispose: release };
  }

  async function revealCard(cardId: number) {
    await nextTick();
    const card = getCardElement(cardId);
    const clip = card?.parentElement?.getBoundingClientRect();
    const rect = card?.getBoundingClientRect();
    const boardRect = boardElement?.getBoundingClientRect();
    if (card && clip && rect && boardRect && (rect.top < clip.top || rect.bottom > clip.bottom
      || rect.left < boardRect.left || rect.right > boardRect.right)) {
      movement.finishAll();
      card.scrollIntoView({ block: 'nearest', inline: 'nearest' });
    }
  }

  return {
    renderedColumns,
    setBoardRef,
    setMovementLayerRef: movement.setLayerRef,
    draftLayoutChanged,
    draftEdited,
    beginCreation,
    revealCard,
    creationTransition: { enter: creationAnimation.enter, leave: creationAnimation.leave, cancel: creationAnimation.cancel },
    gooGroups: goo.gooGroups,
    gooGroupsByColumnId: goo.gooGroupsByColumnId,
    gooBlurStdDeviation: goo.gooBlurStdDeviation,
    gooBlobBorderRadiusPx: goo.gooBlobBorderRadiusPx,
    gooColorMatrixValues: goo.gooColorMatrixValues,
    selectionGooGroup: computed(() => selectionGoo.gooGroups.value[0] ?? null),
    selectionGooGroupsByColumnId: selectionGoo.gooGroupsByColumnId,
    selectionGooBlobBorderRadiusPx: selectionGoo.gooBlobBorderRadiusPx
  };
}

export type BoardCreationMotion = Pick<ReturnType<typeof useBoardMotion>, 'draftLayoutChanged' | 'draftEdited' | 'beginCreation'>;
