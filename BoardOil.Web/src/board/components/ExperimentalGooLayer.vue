<template>
  <canvas ref="canvas" class="experimental-goo-layer" aria-hidden="true" :data-renderer="mode" />
  <Teleport v-for="column in nativeColumns" :key="column.index" :to="column.element">
    <svg v-if="showNativeOutlines" class="goo-layer smooth-column-outline" aria-hidden="true" focusable="false">
      <path v-for="outline in column.outlines" :key="outline.id" :d="outline.d" :fill="outline.colour" fill-rule="evenodd" />
    </svg>
  </Teleport>
  <span ref="colourProbe" class="goo-colour-probe" aria-hidden="true" />
</template>

<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, shallowRef, watch } from 'vue';
import type { BoardColumn, Slick } from '../../shared/types/boardTypes';
import { boardGooClips } from '../utils/gooViewport';
import { buildSlickGooDescriptors } from '../utils/slickGooAdapter';
import { GooScene, type SlickActivity, type SlickAppearance } from '../goo/scene';
import { SmoothLayers, type ColumnOutline } from '../goo/smoothLayers';
import { createLiquidWorker } from '../goo/liquidWorker';
import type { ExperimentalGooMode, GooCard, Viewport } from '../goo/types';

const props = defineProps<{
  mode: ExperimentalGooMode;
  columns: BoardColumn[];
  slicks: Slick[];
  floatingCards: ReadonlyMap<number, HTMLElement>;
  layoutVersion: number;
}>();
const emit = defineEmits<{ activity: [value: SlickActivity[]] }>();
const canvas = ref<HTMLCanvasElement | null>(null);
const colourProbe = ref<HTMLElement | null>(null);
const scene = new GooScene(createLiquidWorker, scheduleFrame);
const smooth = new SmoothLayers();
const nativeColumns = shallowRef<{ index: number; element: HTMLElement; outlines: ColumnOutline[] }[]>([]);
const showNativeOutlines = ref(true);
let board: HTMLElement | null = null;
let context: CanvasRenderingContext2D | null = null;
let resizeObserver: ResizeObserver | null = null;
let themeObserver: MutationObserver | null = null;
const observed = new Set<Element>();
let reducedMotion: MediaQueryList | null = null;
let frameId: number | null = null, last = 0, needsMeasurement = true, renders = 0;
let activitySignature = '';
let disposed = false;
let measuredCards: GooCard[] = [];
let appearances: SlickAppearance[] = [];
let viewport: Viewport | null = null;
const columnScrolls = new Map<HTMLElement, { column: number; left: number; top: number }>();

function onBoardScroll(event: Event) {
  if (!board || !canvas.value || document.hidden) return;
  if (needsMeasurement || !viewport) {
    measure();
  } else if (event.target === board) {
    if (viewport.left === board.scrollLeft && viewport.top === board.scrollTop) return;
    viewport = { ...viewport, left: board.scrollLeft, top: board.scrollTop };
    canvas.value.style.transform = `translate(${viewport.left}px, ${viewport.top}px)`;
    if (props.mode === 'liquid') scene.update(measuredCards, appearances, viewport, 'scroll');
  } else {
    const scroller = event.target as HTMLElement;
    const previous = columnScrolls.get(scroller);
    if (!previous) return;
    const dx = previous.left - scroller.scrollLeft, dy = previous.top - scroller.scrollTop;
    if (!dx && !dy) return;
    measuredCards = measuredCards.map(card => card.column === previous.column && !props.floatingCards.has(card.id)
      ? { ...card, x: card.x + dx, y: card.y + dy } : card);
    previous.left = scroller.scrollLeft; previous.top = scroller.scrollTop;
    if (props.mode === 'liquid') scene.update(measuredCards, appearances, viewport, 'scroll');
  }
  if (props.mode === 'liquid' && !reducedMotion?.matches) {
    scheduleFrame();
    return;
  }
  if (frameId !== null) cancelAnimationFrame(frameId);
  frameId = null;
  frame(performance.now());
}

function scheduleMeasurement() {
  needsMeasurement = true;
  scheduleFrame();
}
function scheduleFrame() {
  if (disposed || !board || document.hidden || frameId !== null) return;
  frameId = requestAnimationFrame(frame);
}
function measure() {
  if (!board || !canvas.value || !context || !colourProbe.value) return;
  const boardRect = board.getBoundingClientRect();
  const left = board.scrollLeft, top = board.scrollTop;
  const originX = boardRect.left + board.clientLeft - left;
  const originY = boardRect.top + board.clientTop - top;
  const width = board.clientWidth, height = board.clientHeight;
  const elements = new Map<number, HTMLElement>();
  board.querySelectorAll<HTMLElement>('.column-content > .card[data-card-id]').forEach(el => elements.set(Number(el.dataset.cardId), el));
  props.floatingCards.forEach((el, id) => elements.set(id, el));
  const cards: GooCard[] = [];
  const nextObserved = new Set<Element>([board]);
  props.columns.forEach((column, index) => {
    for (const card of column.cards) {
      const element = elements.get(card.id);
      if (!element?.isConnected) continue;
      nextObserved.add(element);
      const rect = element.getBoundingClientRect();
      if (rect.width <= 0 || rect.height <= 0) continue;
      const radius = Number.parseFloat(getComputedStyle(element).borderTopLeftRadius) || 0;
      cards.push({ id: card.id, group: card.slickId ?? null, column: index,
        x: rect.left - originX + rect.width / 2, y: rect.top - originY + rect.height / 2,
        width: rect.width, height: rect.height, radius });
    }
  });
  let clipTop = top, clipBottom = top + height;
  const clips = [...board.querySelectorAll<HTMLElement>('.column-content')];
  columnScrolls.clear();
  for (const clip of clips) {
    const column = props.columns.findIndex(c => c.id === Number(clip.dataset.columnId));
    if (column >= 0) columnScrolls.set(clip, { column, left: clip.scrollLeft, top: clip.scrollTop });
  }
  if (clips.length) {
    const rects = clips.map(el => { nextObserved.add(el); return el.getBoundingClientRect(); });
    clipTop = Math.max(top, Math.min(...rects.map(r => r.top - originY)));
    clipBottom = Math.min(top + height, Math.max(...rects.map(r => r.bottom - originY)));
  }
  const sharedClip = boardGooClips.get(board);
  if (sharedClip) { clipTop = top + sharedClip.top; clipBottom = top + sharedClip.bottom; }
  for (const element of observed) if (!nextObserved.has(element)) { resizeObserver?.unobserve(element); observed.delete(element); }
  for (const element of nextObserved) if (!observed.has(element)) { resizeObserver?.observe(element); observed.add(element); }
  const slicks = new Map(props.slicks.map(slick => [slick.id, slick]));
  const descriptors = buildSlickGooDescriptors(props.columns, slicks);
  appearances = [...new Map(descriptors.map(d => [d.groupKey, d])).values()].map(descriptor => {
    const id = Number(descriptor.groupKey.slice('slick-'.length));
    colourProbe.value!.style.color = descriptor.colour;
    return { id, name: slicks.get(id)?.name ?? 'Slick', colour: getComputedStyle(colourProbe.value!).color };
  });
  const ratio = window.devicePixelRatio || 1;
  const pixelWidth = Math.round(width * ratio), pixelHeight = Math.round(height * ratio);
  if (canvas.value.width !== pixelWidth) canvas.value.width = pixelWidth;
  if (canvas.value.height !== pixelHeight) canvas.value.height = pixelHeight;
  canvas.value.style.width = `${width}px`; canvas.value.style.height = `${height}px`;
  canvas.value.style.transform = `translate(${left}px, ${top}px)`;
  context.setTransform(ratio, 0, 0, ratio, 0, 0);
  measuredCards = cards;
  viewport = { left, top, width, height, clipTop, clipBottom };
  scene.update(measuredCards, appearances, viewport);
  showNativeOutlines.value = props.mode === 'smooth' || (reducedMotion?.matches ?? false);
  if (showNativeOutlines.value) {
    nativeColumns.value = [...columnScrolls].map(([element, scroll]) => {
      const rect = element.getBoundingClientRect();
      const x = rect.left + element.clientLeft - originX - element.scrollLeft;
      const y = rect.top + element.clientTop - originY - element.scrollTop;
      const local = cards.filter(card => card.column === scroll.column && !props.floatingCards.has(card.id))
        .map(card => ({ ...card, x: card.x - x, y: card.y - y }));
      return { index: scroll.column, element, outlines: smooth.columnOutlines(scroll.column, local, appearances) };
    });
    smooth.retainColumns(new Set(nativeColumns.value.map(c => c.index)));
  }
  needsMeasurement = false;
}
function frame(_timestamp: number) {
  const now = performance.now();
  frameId = null;
  if (!context || !canvas.value || document.hidden) { last = 0; return; }
  const elapsed = last ? (now - last) / 1000 : 0;
  last = now;
  if (needsMeasurement) measure();
  const simulate = props.mode === 'liquid' && !reducedMotion?.matches;
  if (simulate) scene.step(elapsed);
  if (simulate) scene.draw(context, props.mode, false);
  else if (viewport) smooth.draw(context, measuredCards, appearances, viewport, props.floatingCards);
  canvas.value.dataset.outlineBuilds = String(smooth.outlineBuilds);
  const activity = scene.activity;
  const signature = JSON.stringify(activity);
  if (signature !== activitySignature) { activitySignature = signature; emit('activity', activity); }
  canvas.value.dataset.renderCount = String(++renders);
  canvas.value.dataset.awake = String(activity.filter(slick => !slick.sleeping).length);
  canvas.value.dataset.slicks = JSON.stringify(scene.diagnostics);
  canvas.value.dataset.simulation = scene.simulationBackend;
  if (simulate && scene.awake) scheduleFrame();
  else last = 0;
}
function visibilityChanged() {
  last = 0;
  if (document.hidden) {
    scene.pause();
    if (frameId !== null) cancelAnimationFrame(frameId);
    frameId = null;
  } else scheduleMeasurement();
}
function motionPreferenceChanged() { scene.pause(); scene.wake(); last = 0; scheduleMeasurement(); }
watch(() => [props.columns, props.slicks, props.layoutVersion, [...props.floatingCards]], scheduleMeasurement, { deep: true, flush: 'post' });
watch(() => props.mode, () => { scene.pause(); last = 0; scheduleMeasurement(); }, { flush: 'post' });
onMounted(() => {
  board = canvas.value!.parentElement;
  context = canvas.value!.getContext('2d');
  resizeObserver = new ResizeObserver(scheduleMeasurement);
  reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
  reducedMotion.addEventListener('change', motionPreferenceChanged);
  themeObserver = new MutationObserver(scheduleMeasurement);
  themeObserver.observe(document.documentElement, { attributes: true, attributeFilter: ['class', 'style', 'data-theme'] });
  board?.addEventListener('scroll', onBoardScroll, { capture: true, passive: true });
  window.addEventListener('resize', scheduleMeasurement);
  document.addEventListener('visibilitychange', visibilityChanged);
  scheduleMeasurement();
});
onBeforeUnmount(() => {
  disposed = true;
  scene.dispose();
  if (frameId !== null) cancelAnimationFrame(frameId);
  resizeObserver?.disconnect(); themeObserver?.disconnect();
  board?.removeEventListener('scroll', onBoardScroll, true);
  window.removeEventListener('resize', scheduleMeasurement);
  document.removeEventListener('visibilitychange', visibilityChanged);
  reducedMotion?.removeEventListener('change', motionPreferenceChanged);
});
</script>

<style scoped>
.experimental-goo-layer { position: absolute; left: 0; top: 0; pointer-events: none; z-index: 2; }
.smooth-column-outline { position: absolute; inset: 0; width: 100%; height: 100%; overflow: visible; pointer-events: none; z-index: 0; }
.goo-colour-probe { position: absolute; visibility: hidden; pointer-events: none; }
</style>
