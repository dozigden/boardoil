import { computed, nextTick, onBeforeUnmount, shallowReactive, watch, type ComputedRef, type Ref } from 'vue';
import type { BoardColumn, Card, Slick } from '../../shared/types/boardTypes';
import { useGooLayer } from './useGooLayer';
import { useBoardCardRemovals } from './useBoardCardRemovals';
import { useCardLayoutAnimation, type CardLayoutEffect } from './useCardLayoutAnimation';
import { useCardMovementAnimation, type CardMovementSnapshot } from './useCardMovementAnimation';
import { findMovedColumns, type CardIdsByColumn } from '../utils/cardMovement';
import { createBoardAnimationRunner } from '../utils/boardAnimationRunner';
import type { GooRendererMode } from '../utils/gooRendererMode';
import type { CardSearchAndTagFilter } from '../utils/cardFilters';
import { buildSlickGooDescriptors, buildSlickGooMembershipSignature, buildSlickGooStyleSignature } from '../utils/slickGooAdapter';
import { buildSelectionGooDescriptors, buildSelectionGooMembershipSignature, createSelectionGooStyle } from '../utils/selectionGooAdapter';

type LayoutChange = {
  columnIds: Set<number>;
  positions: CardMovementSnapshot;
  effects: Map<HTMLElement, CardLayoutEffect>;
  arrivingIds: Set<number>;
  removingIds: Set<number>;
  resizingIds: Set<number>;
  previousMargins: Map<number, number>;
  replacements: Map<number, number>;
};

export function useBoardMotion(
  columns: ComputedRef<BoardColumn[]>,
  cardIdsByColumnId: ComputedRef<CardIdsByColumn>,
  slicks: Ref<Slick[]>,
  selectedCardIds: Ref<number[]>,
  selectionMode: Ref<boolean>,
  gooRendererMode: GooRendererMode,
  filters: ComputedRef<CardSearchAndTagFilter>
) {
  let boardElement: HTMLElement | null = null;
  let pendingChange: LayoutChange | null = null;
  let disposed = false;
  onBeforeUnmount(() => { disposed = true; pendingChange = null; });
  const allCardIds = computed(() => Object.values(cardIdsByColumnId.value).flat());
  const removals = useBoardCardRemovals();
  const pendingCreations = shallowReactive(new Map<number, Set<number>>());
  // A local create can reach the store through realtime before its HTTP reply.
  // Hold new arrivals in that column until the editor and saved card can be
  // swapped in one render. Existing cards continue receiving updates normally.
  const renderedColumns = computed(() => {
    if (pendingCreations.size === 0) {
      return removals.project(columns.value);
    }
    return removals.project(columns.value.map(column => {
      const knownCardIds = pendingCreations.get(column.id);
      if (!knownCardIds) {
        return column;
      }
      return { ...column, cards: column.cards.filter(card => knownCardIds.has(card.id)) };
    }));
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
  const layoutAnimation = useCardLayoutAnimation(animationRunner);
  // Catalogue refreshes can recreate the filter object without changing the
  // effective filters. They must not settle an unrelated travelling card.
  const filterSignature = computed(() => JSON.stringify(filters.value));

  watch([cardIdsByColumnId, columns, filterSignature], ([current, visible, currentFilters], [previous, previousVisible, previousFilters]) => {
    if (!boardElement || disposed) {
      return;
    }
    const affectedColumns = findMovedColumns(previous, current);
    const currentIds = new Set(Object.values(current).flat());
    const previousIds = new Set(Object.values(previous).flat());
    const previousVisibleIds = new Set(previousVisible.flatMap(column => column.cards.map(card => card.id)));
    // Only canonical removal animates. Filter hides/reveals remain immediate.
    if (currentFilters !== previousFilters) {
      movement.finishAll();
      layoutAnimation.finishCards();
      removals.clear();
    }
    // A realtime arrival held behind a local editor has never been displayed.
    const removedIds = new Set([...previousVisibleIds].filter(id => !currentIds.has(id) && getCardElement(id)));
    for (const column of previousVisible) {
      if (column.cards.some(card => removedIds.has(card.id))) { affectedColumns.add(column.id); }
    }
    const newIds = new Set(Object.values(current).flat().filter(id => !previousIds.has(id)));
    const arrivingIds: number[] = [];
    const resizingIds: number[] = [];
    const previousCards = new Map(previousVisible.flatMap(column => column.cards.map(card => [card.id, card] as const)));
    for (const column of renderedColumns.value) {
      for (const card of column.cards) {
        const previousCard = previousCards.get(card.id);
        if (newIds.has(card.id)) {
          arrivingIds.push(card.id);
          affectedColumns.add(column.id);
        } else if (currentFilters === previousFilters && previousCard && cardContentChanged(previousCard, card)
          && getCardElement(card.id)) {
          resizingIds.push(card.id);
          affectedColumns.add(column.id);
          affectedColumns.add(previousCard.boardColumnId);
        }
      }
    }
    const change = beginLayoutChange(affectedColumns);
    if (!animationRunner.reducedMotion) {
      for (const id of removals.retain(previousVisible, removedIds)) {
        change.removingIds.add(id);
      }
    }
    removals.prune(visible, currentIds);
    for (const id of arrivingIds) {
      change.arrivingIds.add(id);
    }
    for (const id of resizingIds) {
      change.resizingIds.add(id);
    }
  });

  function beginLayoutChange(columnIds: Set<number>): LayoutChange {
    if (!pendingChange) {
      pendingChange = {
        columnIds: new Set(), positions: { cards: new Map(), columns: new Map() },
        effects: new Map(), arrivingIds: new Set(), removingIds: new Set(), resizingIds: new Set(),
        previousMargins: new Map(), replacements: new Map()
      };
      // Semantic draft changes call this before changing Vue state. Defer the
      // nextTick lookup so it waits for that render as well as watcher renders.
      const change = pendingChange;
      void Promise.resolve().then(() => finishLayoutChange(change));
    }
    const change = pendingChange;
    const uncaptured = new Set([...columnIds].filter(id => !change.columnIds.has(id)));
    if (uncaptured.size > 0) {
      // Capture both effects before cancelling either changes the layout.
      const effects = layoutAnimation.capture(uncaptured);
      const positions = movement.capture(uncaptured);
      layoutAnimation.suspend(effects);
      for (const id of uncaptured) { change.columnIds.add(id); }
      for (const [id, position] of positions.cards) { change.positions.cards.set(id, position); }
      for (const [id, column] of positions.columns) { change.positions.columns.set(id, column); }
      for (const effect of effects) { change.effects.set(effect.element, effect); }
      for (const card of boardElement?.querySelectorAll<HTMLElement>('.column-content > .card[data-card-id]') ?? []) {
        if (uncaptured.has(Number(card.parentElement!.dataset.columnId))) {
          change.previousMargins.set(Number(card.dataset.cardId), Number.parseFloat(getComputedStyle(card).marginTop));
        }
      }
    }
    return change;
  }

  async function finishLayoutChange(change: LayoutChange) {
    await nextTick();
    if (disposed || pendingChange !== change) {
      return;
    }
    pendingChange = null;
    movement.prune();
    movement.restoreScroll(change.positions);
    const effects = new Map<HTMLElement, CardLayoutEffect>();
    for (const effect of change.effects.values()) {
      let element: HTMLElement | null | undefined = effect.element;
      if (!element.isConnected) {
        const cardId = Number(element.dataset.cardId);
        element = getCardElement(cardId);
      }
      if (element?.isConnected) {
        effects.set(element, layoutAnimation.retarget(effect, element));
      }
    }
    // Measure every endpoint before any creation effect collapses its element.
    for (const cardId of change.arrivingIds) {
      const card = getCardElement(cardId);
      if (card) {
        const followingId = Number((card.nextElementSibling as HTMLElement | null)?.dataset.cardId);
        effects.set(card, layoutAnimation.prepareInsert(card, change.previousMargins.get(followingId) ?? 0));
      }
    }
    for (const [cardId, height] of change.replacements) {
      const card = getCardElement(cardId);
      if (card) {
        effects.set(card, layoutAnimation.prepareReplace(card, height));
      }
    }
    for (const cardId of change.resizingIds) {
      const card = getCardElement(cardId);
      const previous = change.positions.cards.get(cardId);
      // Existing creation/removal/resize effects already retain the displayed
      // box and retarget to the updated content. Do not replace their callbacks.
      if (card && previous && !effects.has(card)
        && Math.abs(card.getBoundingClientRect().height - previous.rect.height) >= 0.5) {
        effects.set(card, layoutAnimation.prepareResize(card, previous.rect.height));
      }
    }
    const removingIds = new Set([
      ...change.removingIds,
      ...[...effects.keys()].map(element => Number(element.dataset.cardId)).filter(removals.has)
    ]);
    for (const id of removingIds) {
      const card = getCardElement(id);
      if (card && removals.has(id)) {
        const siblings = [...card.parentElement!.children].filter(element => element.matches('.card, .inline-card-slot, .column-filter-empty'));
        // Each disappearing item cancels one flex gap, except the final item
        // when the whole column is becoming empty.
        const cancelGap = siblings.some(element => !removals.has(Number((element as HTMLElement).dataset.cardId)))
          || siblings[0] !== card;
        const captured = effects.get(card);
        const effect = layoutAnimation.prepareRemoval(card, cancelGap, () => removals.remove(id), captured);
        effects.set(card, effect);
      }
    }
    const plans = [...effects.values()];
    const targets = layoutAnimation.withFinalLayout(plans, () => movement.measureTargets(change.positions));
    const resizingColumns = new Set(plans.filter(effect => change.positions.cards.has(Number(effect.element.dataset.cardId)))
      .map(effect => Number(effect.element.parentElement!.dataset.columnId)));
    for (const effect of plans) {
      layoutAnimation.play(effect);
    }
    // A natural shrinking box may clamp scroll before its starting height is
    // reinstated. Restore again now that all starting boxes exist.
    movement.restoreScroll(change.positions);
    movement.play(change.positions, targets, { resizingColumns, effects: plans });
  }

  function changeDraftLayout(columnId: number, change: () => void) {
    if (disposed) {
      return;
    }
    beginLayoutChange(new Set([columnId]));
    change();
  }

  function queueCreationEffect(effect: CardLayoutEffect) {
    if (disposed) {
      return;
    }
    const columnId = Number(effect.element.parentElement!.dataset.columnId);
    beginLayoutChange(new Set([columnId])).effects.set(effect.element, effect);
  }

  function enterDraft(element: Element, done: () => void) {
    queueCreationEffect(layoutAnimation.prepareEnter(element, done));
  }

  function leaveDraft(element: Element, done: () => void) {
    const effect = layoutAnimation.prepareLeave(element, done);
    if (effect) {
      queueCreationEffect(effect);
    }
  }

  function cancelDraftTransition(element: Element) {
    const slot = element as HTMLElement;
    const pending = pendingChange?.effects.get(slot);
    if (pending) {
      layoutAnimation.preserve(pending);
      pendingChange!.effects.delete(slot);
    }
    layoutAnimation.cancel(slot);
  }

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
    if (disposed) {
      return;
    }
    goo.scheduleGooStructureRefresh();
    selectionGoo.scheduleGooStructureRefresh();
  }

  function draftEdited(columnId: number) {
    const slot = getDraftSlot(columnId);
    if (slot) {
      const pending = pendingChange?.effects.get(slot);
      pendingChange?.effects.delete(slot);
      pending?.done();
      layoutAnimation.finish(slot);
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
      if (disposed) {
        return;
      }
      const currentSlot = getDraftSlot(columnId);
      const cardIsVisible = columns.value.some(column => column.id === columnId
        && column.cards.some(card => card.id === cardId));
      const replacementHeight = currentSlot && cardIsVisible
        ? layoutAnimation.prepareReplacement(currentSlot)
        : null;
      const change = beginLayoutChange(new Set([columnId]));
      if (replacementHeight !== null && cardId !== null) {
        change.replacements.set(cardId, replacementHeight);
      }
      release();
      closeDraft();
      await nextTick();
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
    isRemovingCard: removals.has,
    resolveCardBoundaryClass: removals.boundaryClass,
    setBoardRef,
    setMovementLayerRef: movement.setLayerRef,
    draftLayoutChanged,
    draftEdited,
    changeDraftLayout,
    beginCreation,
    revealCard,
    creationTransition: { enter: enterDraft, leave: leaveDraft, cancel: cancelDraftTransition },
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

export type BoardCreationMotion = Pick<ReturnType<typeof useBoardMotion>, 'draftLayoutChanged' | 'draftEdited' | 'changeDraftLayout' | 'beginCreation'>;

function cardContentChanged(previous: Card, current: Card) {
  if (previous === current) { return false; }
  // Ignore ordering, timestamps and styles: duplicate delivery and moves must
  // not restart a box effect. Measure only saved content that can change height.
  return previous.title !== current.title || previous.cardTypeId !== current.cardTypeId
    || previous.assignedUserDisplayName !== current.assignedUserDisplayName
    || previous.completedChecklistItemCount !== current.completedChecklistItemCount
    || previous.totalChecklistItemCount !== current.totalChecklistItemCount
    || previous.tags.length !== current.tags.length
    || previous.tags.some((tag, index) => tag.id !== current.tags[index]?.id);
}
