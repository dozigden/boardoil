import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch, type ComputedRef } from 'vue';
import { gooConfig } from '../utils/gooConfig';
import { buildGooGroups, type GooItem, type GooRenderGroup, type RectLike } from '../utils/gooLayout';
import {
  buildTrackedCardGeometry,
  intersectsExpandedRect,
  projectTrackedCardRect,
  resolveGooCullingMarginPx,
  type TrackedGooCardGeometry
} from '../utils/gooGeometry';

export type GooLayerDescriptor = {
  cardId: number;
  columnId: number;
  itemId: string;
  groupKey: string;
  colour: string;
};

type GooRefreshReason = 'geometry' | 'structure' | 'styles';

type TrackedGooCard = {
  cardId: number;
  columnId: number;
  itemId: string;
  groupKey: string;
  colour: string;
  cardElement: HTMLElement;
  clipElement: HTMLElement | null;
  geometry: TrackedGooCardGeometry | null;
};

type ClipPadding = {
  left: number;
  right: number;
  top: number;
  bottom: number;
};

type GooPerfSample = {
  frameCount: number;
  refreshMs: number;
  buildMs: number;
  itemCount: number;
};

export function useGooLayer(
  descriptors: ComputedRef<GooLayerDescriptor[]>,
  structureSignature: ComputedRef<string>,
  styleSignature: ComputedRef<string>
) {
  const gooGroups = ref<GooRenderGroup[]>([]);
  const gooGroupsByColumnId = ref(new Map<number, GooRenderGroup[]>());
  const gooBlurStdDeviation = gooConfig.blurStdDeviation;
  const gooBlobBorderRadiusPx = gooConfig.blobBorderRadiusPx;
  const gooColorMatrixValues = computed(() =>
    `1 0 0 0 0
0 1 0 0 0
0 0 1 0 0
0 0 0 ${gooConfig.alphaMultiplier} ${gooConfig.alphaOffset}`
  );

  const boardElement = ref<HTMLElement | null>(null);
  const gooPerfDebugEnabled = resolveGooPerfDebugEnabled();
  let gooRafId: number | null = null;
  let queuedRefreshReason: GooRefreshReason | null = null;
  let gooStructureDirty = true;
  let gooStylesDirty = true;
  const gooCardElementCache = new Map<number, HTMLElement>();
  const clipPaddingCache = new Map<HTMLElement, ClipPadding>();
  const observedGooCardElements = new Set<HTMLElement>();
  let cardResizeObserver: ResizeObserver | null = null;
  let perfSample: GooPerfSample = {
    frameCount: 0,
    refreshMs: 0,
    buildMs: 0,
    itemCount: 0
  };
  let lastPerfLogMs = 0;
  let trackedGooCards: TrackedGooCard[] = [];

  function setBoardRef(element: unknown) {
    if (boardElement.value) {
      boardElement.value.removeEventListener('scroll', onBoardScroll, true);
    }

    if (!(element instanceof HTMLElement)) {
      boardElement.value = null;
      gooGroups.value = [];
      gooGroupsByColumnId.value = new Map();
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
    const refreshStart = gooPerfDebugEnabled ? performance.now() : 0;
    const boardSurface = boardElement.value;
    if (!boardSurface) {
      gooGroups.value = [];
      gooGroupsByColumnId.value = new Map();
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
      gooGroupsByColumnId.value = new Map();
      return;
    }

    const boardRect = boardSurface.getBoundingClientRect();
    const items: GooItem[] = [];
    const localItemsByColumnId = new Map<number, GooItem[]>();
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
      if (reason !== 'geometry' && trackedCard.clipElement) {
        const localRect = resolveLocalCardRect(rect, trackedCard.clipElement);
        const localItems = getOrCreateLocalItems(localItemsByColumnId, trackedCard.columnId);
        localItems.push({
          id: trackedCard.itemId,
          groupKey: trackedCard.groupKey,
          colour: trackedCard.colour,
          rect: localRect,
          clipRect: null
        });
      }

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
      if (reason !== 'geometry') {
        gooGroupsByColumnId.value = buildColumnGooGroups(localItemsByColumnId);
      }
      if (gooPerfDebugEnabled) {
        recordPerfSample(performance.now() - refreshStart, 0, 0);
      }
      return;
    }

    const buildStart = gooPerfDebugEnabled ? performance.now() : 0;
    gooGroups.value = buildGooGroups(items, boardRect, gooConfig, {
      left: boardSurface.scrollLeft,
      top: boardSurface.scrollTop
    });
    if (reason !== 'geometry') {
      gooGroupsByColumnId.value = buildColumnGooGroups(localItemsByColumnId);
    }
    if (gooPerfDebugEnabled) {
      const buildMs = performance.now() - buildStart;
      const refreshMs = performance.now() - refreshStart;
      recordPerfSample(refreshMs, buildMs, items.length);
    }
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
        columnId: descriptor.columnId,
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

  function getOrCreateLocalItems(itemsByColumnId: Map<number, GooItem[]>, columnId: number): GooItem[] {
    const existing = itemsByColumnId.get(columnId);
    if (existing) {
      return existing;
    }

    const created: GooItem[] = [];
    itemsByColumnId.set(columnId, created);
    return created;
  }

  function buildColumnGooGroups(itemsByColumnId: Map<number, GooItem[]>): Map<number, GooRenderGroup[]> {
    const localBoardRect: RectLike = {
      left: 0,
      top: 0,
      width: 0,
      height: 0
    };
    const groupsByColumnId = new Map<number, GooRenderGroup[]>();
    for (const [columnId, localItems] of itemsByColumnId) {
      groupsByColumnId.set(columnId, buildGooGroups(localItems, localBoardRect, gooConfig));
    }

    return groupsByColumnId;
  }

  function resolveLocalCardRect(cardRect: RectLike, clipElement: HTMLElement): RectLike {
    const clipRect = clipElement.getBoundingClientRect();
    return {
      left: cardRect.left - clipRect.left + clipElement.scrollLeft,
      top: cardRect.top - clipRect.top + clipElement.scrollTop,
      width: cardRect.width,
      height: cardRect.height
    };
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
      return projectTrackedCardRect(clipRect, trackedCard.geometry, clipElement.scrollLeft, clipElement.scrollTop);
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
    trackedCard.geometry = buildTrackedCardGeometry(cardRect, clipRect, clipElement.scrollLeft, clipElement.scrollTop);
  }

  function measureTrackedCardGeometry(
    cardElement: HTMLElement,
    clipElement: HTMLElement,
    clipRectByElement: Map<HTMLElement, RectLike>
  ): TrackedGooCardGeometry {
    const cardRect = cardElement.getBoundingClientRect();
    const clipRect = resolveClipContentRect(clipElement, clipRectByElement);
    return buildTrackedCardGeometry(cardRect, clipRect, clipElement.scrollLeft, clipElement.scrollTop);
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
    return resolveGooCullingMarginPx(gooConfig);
  }

  function recordPerfSample(refreshMs: number, buildMs: number, itemCount: number) {
    perfSample.frameCount += 1;
    perfSample.refreshMs += refreshMs;
    perfSample.buildMs += buildMs;
    perfSample.itemCount += itemCount;

    const now = performance.now();
    if ((now - lastPerfLogMs) < 1_000) {
      return;
    }

    const avgRefresh = perfSample.refreshMs / perfSample.frameCount;
    const avgBuild = perfSample.buildMs / perfSample.frameCount;
    const avgItems = perfSample.itemCount / perfSample.frameCount;
    console.log('[goo-perf] avg(ms)', {
      avgRefresh: Number(avgRefresh.toFixed(2)),
      avgBuild: Number(avgBuild.toFixed(2)),
      avgItems: Number(avgItems.toFixed(1)),
      samples: perfSample.frameCount
    });

    perfSample = {
      frameCount: 0,
      refreshMs: 0,
      buildMs: 0,
      itemCount: 0
    };
    lastPerfLogMs = now;
  }

  function resolveGooPerfDebugEnabled() {
    if (import.meta.env.DEV) {
      try {
        const localStorageValue = globalThis.localStorage?.getItem('boardoil:goo-perf-debug');
        if (localStorageValue === '1' || localStorageValue === 'true') {
          return true;
        }
      } catch {
        // Ignore localStorage access errors and continue.
      }

      const search = typeof window !== 'undefined' ? window.location?.search ?? '' : '';
      if (search.includes('gooPerfDebug=1') || search.includes('gooPerfDebug=true')) {
        return true;
      }
    }

    const envValue = (import.meta.env.VITE_GOO_PERF_DEBUG as string | undefined)?.trim().toLowerCase() ?? '';
    return envValue === '1' || envValue === 'true';
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
    gooGroupsByColumnId,
    gooBlurStdDeviation,
    gooBlobBorderRadiusPx,
    gooColorMatrixValues,
    setBoardRef,
    scheduleGooStructureRefresh
  };
}
