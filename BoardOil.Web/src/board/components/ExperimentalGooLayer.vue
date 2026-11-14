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
import { BoardGooGeometry } from '../goo/boardGeometry';
import { buildSlickGooDescriptors } from '../utils/slickGooAdapter';
import { GooScene, type SlickAppearance } from '../goo/scene';
import { SmoothLayers, type ColumnOutline } from '../goo/smoothLayers';
import { createLiquidWorker } from '../goo/liquidWorker';
import type { ExperimentalGooMode } from '../goo/types';

const props = defineProps<{
  board: HTMLElement;
  mode: ExperimentalGooMode;
  columns: BoardColumn[];
  slicks: Slick[];
  floatingCards: ReadonlyMap<number, HTMLElement>;
  layoutVersion: number;
}>();
const canvas = ref<HTMLCanvasElement | null>(null);
const colourProbe = ref<HTMLElement | null>(null);
const scene = new GooScene(createLiquidWorker, scheduleFrame);
const smooth = new SmoothLayers();
const nativeColumns = shallowRef<{ index: number; element: HTMLElement; outlines: ColumnOutline[] }[]>([]);
const showNativeOutlines = ref(true);
let board: HTMLElement | null = null;
let context: CanvasRenderingContext2D | null = null;
let geometry: BoardGooGeometry | null = null;
let themeObserver: MutationObserver | null = null;
let reducedMotion: MediaQueryList | null = null;
let frameId: number | null = null, last = 0, needsMeasurement = true, renders = 0;
let disposed = false;
let appearances: SlickAppearance[] = [];

function onBoardScroll(event: Event) {
  if (!geometry || !canvas.value || document.hidden) return;
  if (needsMeasurement || !geometry.viewport) {
    measure();
  } else {
    if (!geometry.projectScroll(event.target, props.floatingCards)) return;
    const viewport = geometry.viewport;
    if (props.mode === 'liquid') scene.update(geometry.cards, appearances, viewport, 'scroll');
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
  if (!geometry || !canvas.value || !context || !colourProbe.value) return;
  const { cards, viewport } = geometry.measure(props.columns, props.floatingCards);
  const { width, height } = viewport;
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
  context.setTransform(ratio, 0, 0, ratio, 0, 0);
  scene.update(cards, appearances, viewport);
  showNativeOutlines.value = props.mode === 'smooth' || (reducedMotion?.matches ?? false);
  if (showNativeOutlines.value) {
    nativeColumns.value = geometry.localColumns(props.floatingCards).map(column => ({
      index: column.index, element: column.element,
      outlines: smooth.columnOutlines(column.index, column.cards, appearances)
    }));
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
  if (simulate) scene.draw(context);
  else if (geometry?.viewport) smooth.draw(context, geometry.cards, appearances, geometry.viewport, props.floatingCards);
  canvas.value.dataset.outlineBuilds = String(smooth.outlineBuilds);
  const diagnostics = scene.diagnostics;
  canvas.value.dataset.renderCount = String(++renders);
  canvas.value.dataset.awake = String(diagnostics.filter(slick => !slick.sleeping).length);
  canvas.value.dataset.slicks = JSON.stringify(diagnostics);
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
  board = props.board;
  context = canvas.value!.getContext('2d');
  if (board) geometry = new BoardGooGeometry(board, scheduleMeasurement);
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
  geometry?.dispose(); themeObserver?.disconnect();
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
