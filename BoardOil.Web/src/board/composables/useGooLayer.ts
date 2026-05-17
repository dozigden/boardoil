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
  let gooStructureDirty = true;
  let gooStylesDirty = true;
  const gooCardElementCache = new Map<number, HTMLElement>();
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
    scheduleGooRefresh('geometry');
  }

  function scheduleGooRefresh(reason: GooRefreshReason = 'geometry') {
    if (reason === 'structure') {
      gooStructureDirty = true;
      gooStylesDirty = true;
    } else if (reason === 'styles') {
      gooStylesDirty = true;
    }

    if (gooRafId !== null) {
      cancelAnimationFrame(gooRafId);
    }

    gooRafId = requestAnimationFrame(() => {
      gooRafId = null;
      refreshGooLayer();
    });
  }

  function refreshGooLayer() {
    const boardSurface = boardElement.value;
    if (!boardSurface) {
      gooGroups.value = [];
      trackedGooCards = [];
      gooCardElementCache.clear();
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
    let sawDetachedCard = false;
    for (const trackedCard of trackedGooCards) {
      if (!trackedCard.cardElement.isConnected) {
        gooCardElementCache.delete(trackedCard.cardId);
        sawDetachedCard = true;
        continue;
      }

      if (trackedCard.clipElement && !trackedCard.clipElement.isConnected) {
        trackedCard.clipElement = trackedCard.cardElement.closest<HTMLElement>('.column-content');
      }

      const rect = trackedCard.cardElement.getBoundingClientRect();
      const columnContentRect = trackedCard.clipElement
        ? resolveClipContentRect(trackedCard.clipElement, clipRectByElement)
        : null;
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
      nextTrackedCards.push({
        cardId: descriptor.cardId,
        itemId: descriptor.itemId,
        groupKey: descriptor.groupKey,
        colour: descriptor.colour,
        cardElement,
        clipElement
      });
    }

    trackedGooCards = nextTrackedCards;
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
    const style = getComputedStyle(clipElement);
    const paddingLeft = Number.parseFloat(style.paddingLeft) || 0;
    const paddingRight = Number.parseFloat(style.paddingRight) || 0;
    const paddingTop = Number.parseFloat(style.paddingTop) || 0;
    const paddingBottom = Number.parseFloat(style.paddingBottom) || 0;

    const contentRect: RectLike = {
      left: rect.left + paddingLeft - gooConfig.clipHorizontalInsetPx,
      top: rect.top + paddingTop,
      width: Math.max(0, rect.width - paddingLeft - paddingRight + (gooConfig.clipHorizontalInsetPx * 2)),
      height: Math.max(0, rect.height - paddingTop - paddingBottom)
    };
    cache.set(clipElement, contentRect);
    return contentRect;
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
