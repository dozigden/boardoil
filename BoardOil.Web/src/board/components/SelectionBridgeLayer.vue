<template>
  <canvas ref="canvas" class="selection-bridge-layer selection-bridge-pulse" aria-hidden="true" data-renderer="bridges" />
  <span ref="colourProbe" class="selection-colour-probe" aria-hidden="true" />
</template>

<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, watch } from 'vue';
import type { BoardColumn } from '../../shared/types/boardTypes';
import { BoardGooGeometry } from '../goo/boardGeometry';
import { BridgeLayers } from '../goo/bridgeLayers';
import type { SlickAppearance } from '../goo/scene';
import { buildSelectionBridgeCards, SELECTION_GROUP_ID } from '../utils/selectionGooAdapter';

const props = defineProps<{
  board: HTMLElement;
  columns: BoardColumn[];
  selectedCardIds: number[];
  floatingCards: ReadonlyMap<number, HTMLElement>;
  layoutVersion: number;
}>();
const canvas = ref<HTMLCanvasElement | null>(null);
const colourProbe = ref<HTMLElement | null>(null);
const bridges = new BridgeLayers();
let geometry: BoardGooGeometry | null = null;
let context: CanvasRenderingContext2D | null = null;
let themeObserver: MutationObserver | null = null;
let frameId: number | null = null;
let needsMeasurement = true, disposed = false;
let appearances: SlickAppearance[] = [];
let columnPaths: { element: HTMLElement; left: number; top: number; paths: Path2D[] }[] = [];
let columnHeaders: { left: number; right: number; bottom: number }[] = [];

function scheduleMeasurement() {
  needsMeasurement = true;
  if (!disposed && !document.hidden && frameId === null) frameId = requestAnimationFrame(draw);
}

function measure() {
  if (!geometry || !canvas.value || !context || !colourProbe.value) return;
  const { cards, viewport } = geometry.measure(props.columns, props.floatingCards);
  geometry.cards = buildSelectionBridgeCards(cards, new Set(props.selectedCardIds));
  appearances = [{ id: SELECTION_GROUP_ID, name: 'Selection', colour: getComputedStyle(colourProbe.value).color }];
  const ratio = window.devicePixelRatio || 1;
  const width = Math.round(viewport.width * ratio), height = Math.round(viewport.height * ratio);
  if (canvas.value.width !== width) canvas.value.width = width;
  if (canvas.value.height !== height) canvas.value.height = height;
  canvas.value.style.width = `${viewport.width}px`; canvas.value.style.height = `${viewport.height}px`;
  context.setTransform(ratio, 0, 0, ratio, 0, 0);
  const measuredCards = new Map(geometry.cards.map(card => [card.id, card]));
  const columns = geometry.localColumns(props.floatingCards);
  const boardRect = props.board.getBoundingClientRect();
  const originX = boardRect.left + props.board.clientLeft - viewport.left;
  const originY = boardRect.top + props.board.clientTop - viewport.top;
  columnHeaders = columns.map(column => {
    const panel = column.element.closest('.column')!.getBoundingClientRect();
    const content = column.element.getBoundingClientRect();
    return { left: panel.left - originX, right: panel.right - originX,
      bottom: content.top + column.element.clientTop - originY };
  });
  columnPaths = columns.flatMap(column => {
    if (!column.cards.length) return [];
    const local = column.cards[0], measured = measuredCards.get(local.id)!;
    return [{ element: column.element,
      left: measured.centreX - local.centreX + column.element.scrollLeft,
      top: measured.centreY - local.centreY + column.element.scrollTop,
      paths: bridges.columnOutlines(column.index, column.cards, appearances).map(outline => new Path2D(outline.d)) }];
  });
  bridges.retainColumns(new Set(columns.map(column => column.index)));
  canvas.value.dataset.selectedCardIds = JSON.stringify(geometry.cards.filter(card => card.group === SELECTION_GROUP_ID).map(card => card.id));
  canvas.value.dataset.outlineBuilds = String(bridges.outlineBuilds);
  needsMeasurement = false;
}

function draw() {
  frameId = null;
  if (disposed || document.hidden) return;
  if (needsMeasurement) measure();
  if (!context || !geometry?.viewport) return;
  const viewport = geometry.viewport;
  // Exclude each header before drawing either cached outlines or bridge shoulders.
  const clip = new Path2D();
  clip.rect(0, viewport.clipTop - viewport.top, viewport.width, Math.max(0, viewport.clipBottom - viewport.clipTop));
  for (const header of columnHeaders) {
    clip.rect(header.left - viewport.left, viewport.clipTop - viewport.top, header.right - header.left,
      Math.max(0, Math.min(header.bottom, viewport.clipBottom) - viewport.clipTop));
  }
  context.clearRect(0, 0, viewport.width, viewport.height);
  context.save();
  context.clip(clip, 'evenodd');
  bridges.draw(context, geometry.cards, appearances, viewport, props.floatingCards);
  // Composite opaque paths together before CSS applies the selection pulse.
  // Separate translucent canvas/SVG layers darken overlapping shoulders.
  context.translate(-viewport.left, -viewport.top);
  context.fillStyle = appearances[0].colour;
  for (const column of columnPaths) {
    context.save();
    context.translate(column.left - column.element.scrollLeft, column.top - column.element.scrollTop);
    for (const path of column.paths) context.fill(path, 'evenodd');
    context.restore();
  }
  context.restore();
}

function onBoardScroll(event: Event) {
  if (!geometry || document.hidden) return;
  if (!needsMeasurement && !geometry.projectScroll(event.target, props.floatingCards)) return;
  if (frameId !== null) cancelAnimationFrame(frameId);
  draw();
}

function visibilityChanged() {
  if (document.hidden) {
    if (frameId !== null) cancelAnimationFrame(frameId);
    frameId = null;
  } else scheduleMeasurement();
}

watch(() => [props.columns, props.selectedCardIds, props.layoutVersion, [...props.floatingCards]], scheduleMeasurement, { deep: true, flush: 'post' });
onMounted(() => {
  context = canvas.value!.getContext('2d');
  geometry = new BoardGooGeometry(props.board, scheduleMeasurement);
  themeObserver = new MutationObserver(scheduleMeasurement);
  themeObserver.observe(document.documentElement, { attributes: true, attributeFilter: ['class', 'style', 'data-theme'] });
  props.board.addEventListener('scroll', onBoardScroll, true);
  window.addEventListener('resize', scheduleMeasurement);
  document.addEventListener('visibilitychange', visibilityChanged);
  scheduleMeasurement();
});
onBeforeUnmount(() => {
  disposed = true;
  if (frameId !== null) cancelAnimationFrame(frameId);
  geometry?.dispose(); themeObserver?.disconnect();
  props.board.removeEventListener('scroll', onBoardScroll, true);
  window.removeEventListener('resize', scheduleMeasurement);
  document.removeEventListener('visibilitychange', visibilityChanged);
});
</script>

<style scoped>
.selection-bridge-layer { position: absolute; left: 0; top: 0; pointer-events: none; z-index: 4; }
.selection-colour-probe { position: absolute; visibility: hidden; pointer-events: none; color: var(--bo-selection-accent); }
.selection-bridge-pulse { opacity: 0.34; animation: selection-bridge-pulse 1.5s ease-in-out infinite alternate; }
@keyframes selection-bridge-pulse {
  from { opacity: 0.34; }
  to { opacity: 0.56; }
}
@media (prefers-reduced-motion: reduce) {
  .selection-bridge-pulse { animation: none; opacity: 0.7; }
}
</style>
