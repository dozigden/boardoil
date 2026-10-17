import { computed, nextTick, onBeforeUnmount, shallowReactive, watch, type ComputedRef, type Ref } from 'vue';
import type { BoardColumn, Slick } from '../../shared/types/boardTypes';
import { useGooLayer } from './useGooLayer';
import { useCardCreationAnimation, type CardCreationEffect } from './useCardCreationAnimation';
import { useCardMovementAnimation, type CardMovementSnapshot } from './useCardMovementAnimation';
import { findMovedColumns, type CardIdsByColumn } from '../utils/cardMovement';
import { createBoardAnimationRunner } from '../utils/boardAnimationRunner';
import type { GooRendererMode } from '../utils/gooRendererMode';
import { buildSlickGooDescriptors, buildSlickGooMembershipSignature, buildSlickGooStyleSignature } from '../utils/slickGooAdapter';
import { buildSelectionGooDescriptors, buildSelectionGooMembershipSignature, createSelectionGooStyle } from '../utils/selectionGooAdapter';

type LayoutChange = {
  columnIds: Set<number>;
  positions: CardMovementSnapshot;
  effects: Map<HTMLElement, CardCreationEffect>;
  arrivingIds: Set<number>;
  previousMargins: Map<number, number>;
  replacements: Map<number, number>;
};

export function useBoardMotion(
  columns: ComputedRef<BoardColumn[]>,
  cardIdsByColumnId: ComputedRef<CardIdsByColumn>,
  slicks: Ref<Slick[]>,
  selectedCardIds: Ref<number[]>,
  selectionMode: Ref<boolean>,
  gooRendererMode: GooRendererMode
) {
  let boardElement: HTMLElement | null = null;
  let pendingChange: LayoutChange | null = null;
  let disposed = false;
  onBeforeUnmount(() => { disposed = true; pendingChange = null; });
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

  watch([cardIdsByColumnId, renderedColumns], ([current], [previous]) => {
    if (!boardElement || disposed) {
      return;
    }
    const affectedColumns = findMovedColumns(previous, current);
    const previousIds = new Set(Object.values(previous).flat());
    const newIds = new Set(Object.values(current).flat().filter(id => !previousIds.has(id)));
    const arrivingIds: number[] = [];
    for (const column of renderedColumns.value) {
      for (const card of column.cards) {
        if (newIds.has(card.id)) {
          arrivingIds.push(card.id);
          affectedColumns.add(column.id);
        }
      }
    }
    const change = beginLayoutChange(affectedColumns);
    for (const id of arrivingIds) {
      change.arrivingIds.add(id);
    }
  });

  function beginLayoutChange(columnIds: Set<number>): LayoutChange {
    if (!pendingChange) {
      pendingChange = {
        columnIds: new Set(), positions: { cards: new Map(), columns: new Map() },
        effects: new Map(), arrivingIds: new Set(), previousMargins: new Map(), replacements: new Map()
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
      const effects = creationAnimation.capture(uncaptured);
      const positions = movement.capture(uncaptured);
      creationAnimation.suspend(effects);
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
    const effects = new Map<HTMLElement, CardCreationEffect>();
    for (const effect of change.effects.values()) {
      let element: HTMLElement | null | undefined = effect.element;
      if (!element.isConnected) {
        const cardId = Number(element.dataset.cardId);
        element = getCardElement(cardId);
      }
      if (element?.isConnected) {
        effects.set(element, creationAnimation.retarget(effect, element));
      }
    }
    // Measure every endpoint before any creation effect collapses its element.
    for (const cardId of change.arrivingIds) {
      const card = getCardElement(cardId);
      if (card) {
        const followingId = Number((card.nextElementSibling as HTMLElement | null)?.dataset.cardId);
        effects.set(card, creationAnimation.prepareInsert(card, change.previousMargins.get(followingId) ?? 0));
      }
    }
    for (const [cardId, height] of change.replacements) {
      const card = getCardElement(cardId);
      if (card) {
        effects.set(card, creationAnimation.prepareReplace(card, height));
      }
    }
    const plans = [...effects.values()];
    const targets = creationAnimation.withFinalLayout(plans, () => movement.measureTargets(change.positions));
    for (const effect of plans) {
      creationAnimation.play(effect);
    }
    movement.play(change.positions, targets);
  }

  function changeDraftLayout(columnId: number, change: () => void) {
    if (disposed) {
      return;
    }
    beginLayoutChange(new Set([columnId]));
    change();
  }

  function queueCreationEffect(effect: CardCreationEffect) {
    if (disposed) {
      return;
    }
    const columnId = Number(effect.element.parentElement!.dataset.columnId);
    beginLayoutChange(new Set([columnId])).effects.set(effect.element, effect);
  }

  function enterDraft(element: Element, done: () => void) {
    queueCreationEffect(creationAnimation.prepareEnter(element, done));
  }

  function leaveDraft(element: Element, done: () => void) {
    const effect = creationAnimation.prepareLeave(element, done);
    if (effect) {
      queueCreationEffect(effect);
    }
  }

  function cancelDraftTransition(element: Element) {
    const slot = element as HTMLElement;
    const pending = pendingChange?.effects.get(slot);
    if (pending) {
      creationAnimation.preserve(pending);
      pendingChange!.effects.delete(slot);
    }
    creationAnimation.cancel(slot);
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
      if (disposed) {
        return;
      }
      const currentSlot = getDraftSlot(columnId);
      const cardIsVisible = columns.value.some(column => column.id === columnId
        && column.cards.some(card => card.id === cardId));
      const replacementHeight = currentSlot && cardIsVisible
        ? creationAnimation.prepareReplacement(currentSlot)
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
