import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch, type ComputedRef } from 'vue';
import { gooConfig } from '../utils/gooConfig';
import { buildGooGroups, type GooItem, type GooRenderGroup, type RectLike } from '../utils/gooLayout';

export type GooLayerDescriptor = {
  cardId: number;
  itemId: string;
  groupKey: string;
  colour: string;
};

type GooRefreshReason = 'geometry' | 'structure' | 'styles';

type TrackedGooCard = {
  cardId: number;
  itemId: string;
  groupKey: string;
  colour: string;
  cardElement: HTMLElement;
  clipElement: HTMLElement | null;
  geometry: TrackedGooCardGeometry | null;
};

type TrackedGooCardGeometry = {
  leftInClip: number;
  topInClip: number;
  width: number;
  height: number;
};

type ClipPadding = {
  left: number;
  right: number;
  top: number;
  bottom: number;
};

export function useGooLayer(
  descriptors: ComputedRef<GooLayerDescriptor[]>,
  structureSignature: ComputedRef<string>,
  styleSignature: ComputedRef<string>
) {
  const gooGroups = ref<GooRenderGroup[]>([]);
  const gooBlurStdDeviation = gooConfig.blurStdDeviation;
  const gooBlobBorderRadiusPx = gooConfig.blobBorderRadiusPx;
  const gooColorMatrixValues = computed(() =>
    `1 0 0 0 0
0 1 0 0 0
0 0 1 0 0
0 0 0 ${gooConfig.alphaMultiplier} ${gooConfig.alphaOffset}`
  );

  const boardElement = ref<HTMLElement | null>(null);
  let gooRafId: number | null = null;
  let queuedRefreshReason: GooRefreshReason | null = null;
  let gooStructureDirty = true;
  let gooStylesDirty = true;
  const gooCardElementCache = new Map<number, HTMLElement>();
  const clipPaddingCache = new Map<HTMLElement, ClipPadding>();
  const observedGooCardElements = new Set<HTMLElement>();
  let cardResizeObserver: ResizeObserver | null = null;
  let trackedGooCards: TrackedGooCard[] = [];

  function setBoardRef(element: unknown) {
    if (boardElement.value) {
      boardElement.value.removeEventListener('scroll', onBoardScroll, true);
    }

    if (!(element instanceof HTMLElement)) {
      boardElement.value = null;
      gooGroups.value = [];
      trackedGooCards = [];
      gooCardElementCache.clear();
      clipPaddingCache.clear();
      clearObservedGooCardElements();
      return;
    }

    boardElement.value = element;
    // Capture scroll from nested column scrollers so goo follows cards while inner columns scroll.
    boardElement.value.addEventListener('scroll', onBoardScroll, { passive: true, capture: true });
    scheduleGooRefresh('structure');
  }

  function scheduleGooStructureRefresh() {
    scheduleGooRefresh('structure');
  }

  function onBoardScroll() {
    scheduleGooRefresh('geometry');
  }

  function onWindowResize() {
    scheduleGooRefresh('structure');
  }

  function scheduleGooRefresh(reason: GooRefreshReason = 'geometry') {
    queuedRefreshReason = mergeRefreshReason(queuedRefreshReason, reason);

    if (reason === 'structure') {
      gooStructureDirty = true;
      gooStylesDirty = true;
      clipPaddingCache.clear();
    } else if (reason === 'styles') {
      gooStylesDirty = true;
    }

    if (gooRafId !== null) {
      return;
    }

    gooRafId = requestAnimationFrame(() => {
      gooRafId = null;
      const refreshReason = queuedRefreshReason ?? 'geometry';
      queuedRefreshReason = null;
      refreshGooLayer(refreshReason);
    });
  }

  function refreshGooLayer(reason: GooRefreshReason) {
    const boardSurface = boardElement.value;
    if (!boardSurface) {
      gooGroups.value = [];
      trackedGooCards = [];
      gooCardElementCache.clear();
      queuedRefreshReason = null;
      gooStructureDirty = true;
      gooStylesDirty = true;
      return;
    }

    if (gooStructureDirty) {
      rebuildTrackedGooCards(boardSurface);
    } else if (gooStylesDirty) {
      refreshTrackedGooColours();
    }

    if (trackedGooCards.length === 0) {
      gooGroups.value = [];
      return;
    }

    const boardRect = boardSurface.getBoundingClientRect();
    const items: GooItem[] = [];
    const clipRectByElement = new Map<HTMLElement, RectLike>();
    const preferFastPath = reason !== 'structure' && !gooStructureDirty;
    let sawDetachedCard = false;
    for (const trackedCard of trackedGooCards) {
      if (!trackedCard.cardElement.isConnected) {
        gooCardElementCache.delete(trackedCard.cardId);
        sawDetachedCard = true;
        continue;
      }

      if (trackedCard.clipElement && !trackedCard.clipElement.isConnected) {
        trackedCard.clipElement = trackedCard.cardElement.closest<HTMLElement>('.column-content');
        trackedCard.geometry = null;
      }

      const rect = resolveCardRectForRefresh(trackedCard, preferFastPath, clipRectByElement);
      if (!rect) {
        continue;
      }

      const columnContentRect = trackedCard.clipElement
        ? resolveClipContentRect(trackedCard.clipElement, clipRectByElement)
        : null;
      if (columnContentRect && !intersectsExpandedRect(rect, columnContentRect, resolveCullingMarginPx())) {
        continue;
      }
      items.push({
        id: trackedCard.itemId,
        groupKey: trackedCard.groupKey,
        colour: trackedCard.colour,
        rect,
        clipRect: columnContentRect
      });
    }

    if (sawDetachedCard) {
      gooStructureDirty = true;
    }

    if (items.length === 0) {
      gooGroups.value = [];
      return;
    }

    gooGroups.value = buildGooGroups(items, boardRect, gooConfig);
  }

  function rebuildTrackedGooCards(boardSurface: HTMLElement) {
    const cardElementsById = new Map<number, HTMLElement>();
    const clipRectByElement = new Map<HTMLElement, RectLike>();
    const cardElements = boardSurface.querySelectorAll<HTMLElement>('.card[data-card-id]');
    for (const cardElement of cardElements) {
      const rawCardId = cardElement.dataset.cardId;
      if (!rawCardId) {
        continue;
      }

      const cardId = Number.parseInt(rawCardId, 10);
      if (!Number.isFinite(cardId)) {
        continue;
      }

      cardElementsById.set(cardId, cardElement);
      gooCardElementCache.set(cardId, cardElement);
    }

    const nextTrackedCards: TrackedGooCard[] = [];
    for (const descriptor of descriptors.value) {
      const cachedCardElement = gooCardElementCache.get(descriptor.cardId);
      const cardElement = cardElementsById.get(descriptor.cardId) ?? cachedCardElement ?? null;
      if (!cardElement || !cardElement.isConnected) {
        gooCardElementCache.delete(descriptor.cardId);
        continue;
      }

      const clipElement = cardElement.closest<HTMLElement>('.column-content');
      const geometry = clipElement
        ? measureTrackedCardGeometry(cardElement, clipElement, clipRectByElement)
        : null;
      nextTrackedCards.push({
        cardId: descriptor.cardId,
        itemId: descriptor.itemId,
        groupKey: descriptor.groupKey,
        colour: descriptor.colour,
        cardElement,
        clipElement,
        geometry
      });
    }

    trackedGooCards = nextTrackedCards;
    syncObservedGooCardElements(nextTrackedCards);
    gooStructureDirty = false;
    gooStylesDirty = false;
  }

  function refreshTrackedGooColours() {
    const colourByCardId = new Map(descriptors.value.map(descriptor => [descriptor.cardId, descriptor.colour] as const));
    for (const trackedCard of trackedGooCards) {
      const nextColour = colourByCardId.get(trackedCard.cardId);
      if (nextColour) {
        trackedCard.colour = nextColour;
      }
    }

    gooStylesDirty = false;
  }

  function resolveClipContentRect(
    clipElement: HTMLElement,
    cache: Map<HTMLElement, RectLike>
  ): RectLike {
    const cached = cache.get(clipElement);
    if (cached) {
      return cached;
    }

    const rect = clipElement.getBoundingClientRect();
    const padding = resolveClipPadding(clipElement);

    const contentRect: RectLike = {
      left: rect.left + padding.left - gooConfig.clipHorizontalInsetPx,
      top: rect.top + padding.top,
      width: Math.max(0, rect.width - padding.left - padding.right + (gooConfig.clipHorizontalInsetPx * 2)),
      height: Math.max(0, rect.height - padding.top - padding.bottom)
    };
    cache.set(clipElement, contentRect);
    return contentRect;
  }

  function resolveCardRectForRefresh(
    trackedCard: TrackedGooCard,
    preferFastPath: boolean,
    clipRectByElement: Map<HTMLElement, RectLike>
  ): RectLike | null {
    const clipElement = trackedCard.clipElement;
    if (preferFastPath && clipElement && trackedCard.geometry) {
      const clipRect = resolveClipContentRect(clipElement, clipRectByElement);
      return {
        left: clipRect.left + trackedCard.geometry.leftInClip - clipElement.scrollLeft,
        top: clipRect.top + trackedCard.geometry.topInClip - clipElement.scrollTop,
        width: trackedCard.geometry.width,
        height: trackedCard.geometry.height
      };
    }

    if (!trackedCard.cardElement.isConnected) {
      return null;
    }

    const rect = trackedCard.cardElement.getBoundingClientRect();
    if (clipElement) {
      updateTrackedCardGeometry(trackedCard, rect, clipElement, clipRectByElement);
    } else {
      trackedCard.geometry = null;
    }
    return rect;
  }

  function updateTrackedCardGeometry(
    trackedCard: TrackedGooCard,
    cardRect: DOMRect | RectLike,
    clipElement: HTMLElement,
    clipRectByElement: Map<HTMLElement, RectLike>
  ) {
    const clipRect = resolveClipContentRect(clipElement, clipRectByElement);
    trackedCard.geometry = {
      leftInClip: cardRect.left - clipRect.left + clipElement.scrollLeft,
      topInClip: cardRect.top - clipRect.top + clipElement.scrollTop,
      width: cardRect.width,
      height: cardRect.height
    };
  }

  function measureTrackedCardGeometry(
    cardElement: HTMLElement,
    clipElement: HTMLElement,
    clipRectByElement: Map<HTMLElement, RectLike>
  ): TrackedGooCardGeometry {
    const cardRect = cardElement.getBoundingClientRect();
    const clipRect = resolveClipContentRect(clipElement, clipRectByElement);
    return {
      leftInClip: cardRect.left - clipRect.left + clipElement.scrollLeft,
      topInClip: cardRect.top - clipRect.top + clipElement.scrollTop,
      width: cardRect.width,
      height: cardRect.height
    };
  }

  function resolveClipPadding(clipElement: HTMLElement): ClipPadding {
    const cached = clipPaddingCache.get(clipElement);
    if (cached) {
      return cached;
    }

    const style = getComputedStyle(clipElement);
    const padding: ClipPadding = {
      left: Number.parseFloat(style.paddingLeft) || 0,
      right: Number.parseFloat(style.paddingRight) || 0,
      top: Number.parseFloat(style.paddingTop) || 0,
      bottom: Number.parseFloat(style.paddingBottom) || 0
    };
    clipPaddingCache.set(clipElement, padding);
    return padding;
  }

  function createCardResizeObserver() {
    if (typeof ResizeObserver === 'undefined' || cardResizeObserver) {
      return;
    }

    cardResizeObserver = new ResizeObserver(() => {
      scheduleGooRefresh('structure');
    });
  }

  function syncObservedGooCardElements(cards: TrackedGooCard[]) {
    if (!cardResizeObserver) {
      return;
    }

    const nextElements = new Set(cards.map(card => card.cardElement));
    for (const existingElement of observedGooCardElements) {
      if (nextElements.has(existingElement)) {
        continue;
      }

      cardResizeObserver.unobserve(existingElement);
      observedGooCardElements.delete(existingElement);
    }

    for (const nextElement of nextElements) {
      if (observedGooCardElements.has(nextElement)) {
        continue;
      }

      cardResizeObserver.observe(nextElement);
      observedGooCardElements.add(nextElement);
    }
  }

  function clearObservedGooCardElements() {
    if (!cardResizeObserver) {
      return;
    }

    for (const element of observedGooCardElements) {
      cardResizeObserver.unobserve(element);
    }
    observedGooCardElements.clear();
  }

  function resolveCullingMarginPx(): number {
    return Math.max(
      24,
      gooConfig.bridgeMaxGapPx,
      Math.abs(gooConfig.widthAdjustPx),
      Math.abs(gooConfig.heightAdjustPx)
    );
  }

  function intersectsExpandedRect(rect: RectLike, clipRect: RectLike, marginPx: number): boolean {
    const left = clipRect.left - marginPx;
    const top = clipRect.top - marginPx;
    const right = clipRect.left + clipRect.width + marginPx;
    const bottom = clipRect.top + clipRect.height + marginPx;

    const rectRight = rect.left + rect.width;
    const rectBottom = rect.top + rect.height;
    if (rectRight < left || rect.left > right) {
      return false;
    }

    if (rectBottom < top || rect.top > bottom) {
      return false;
    }

    return true;
  }

  function mergeRefreshReason(existing: GooRefreshReason | null, incoming: GooRefreshReason): GooRefreshReason {
    if (existing === null) {
      return incoming;
    }

    if (existing === 'structure' || incoming === 'structure') {
      return 'structure';
    }

    if (existing === 'styles' || incoming === 'styles') {
      return 'styles';
    }

    return 'geometry';
  }

  watch(structureSignature, async () => {
    await nextTick();
    scheduleGooRefresh('structure');
  });

  watch(styleSignature, async () => {
    await nextTick();
    scheduleGooRefresh('styles');
  });

  onMounted(() => {
    createCardResizeObserver();
    window.addEventListener('resize', onWindowResize);
  });

  onBeforeUnmount(() => {
    if (boardElement.value) {
      boardElement.value.removeEventListener('scroll', onBoardScroll, true);
    }
    window.removeEventListener('resize', onWindowResize);
    if (gooRafId !== null) {
      cancelAnimationFrame(gooRafId);
      gooRafId = null;
    }
    clearObservedGooCardElements();
    if (cardResizeObserver) {
      cardResizeObserver.disconnect();
      cardResizeObserver = null;
    }
  });

  return {
    gooGroups,
    gooBlurStdDeviation,
    gooBlobBorderRadiusPx,
    gooColorMatrixValues,
    setBoardRef,
    scheduleGooStructureRefresh
  };
}
