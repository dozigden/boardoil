<template>
  <figure class="slick-effect-preview" aria-label="Slick effect preview">
    <figcaption class="preview-caption">Preview</figcaption>
    <div ref="viewport" class="preview-viewport bo-theme-scope" :data-theme="previewTheme" role="img"
      :aria-label="`${effectLabel}: a stationary column on the left and a smoothly scrolling column on the right`">
      <div class="preview-scene">
        <div ref="board" class="preview-board" :class="{ 'preview-board--liquid': mode === 'liquid' }" aria-hidden="true">
          <div v-for="column in columns" :key="column.id" class="column-stack">
            <section class="column">
              <h4 class="preview-column-heading">{{ column.title }}</h4>
              <div class="column-content" :data-column-id="column.id">
                <Card v-for="(card, index) in column.cards" :key="card.id" class="preview-card" :data-card-id="card.id"
                  :class="resolveCardBoundaryClass(column.cards, index)"
                  :card="card" :column-id="column.id" :interactive="false" />
              </div>
            </section>
            <div class="column-tail-drop-zone" />
          </div>
        </div>
        <SlickGooLayer v-if="active && board" :board="board" :mode="mode" :columns="columns" :slicks="slicks"
          :floating-cards="floatingCards" :layout-version="layoutVersion" />
      </div>
    </div>
  </figure>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, useTemplateRef, watch } from 'vue';
import SlickGooLayer from '../../board/components/SlickGooLayer.vue';
import Card from '../../board/components/Card.vue';
import { useBoardColumnClipping } from '../../board/composables/useBoardColumnClipping';
import { resolveCardBoundaryClass } from '../../board/utils/slickCardBoundary';
import type { BoardColumn, Card as BoardCard, Slick } from '../../shared/types/boardTypes';
import type { ThemeMode } from '../../shared/stores/themeStore';
import { slickEffectOptions, type SlickEffect } from '../../shared/types/slickEffect';

const props = defineProps<{ mode: SlickEffect; appearance: ThemeMode }>();
const viewport = useTemplateRef<HTMLElement>('viewport');
const board = useTemplateRef<HTMLElement>('board');
const intersecting = ref(false);
const documentVisible = ref(!document.hidden);
const reducedMotion = ref(false);
const systemDark = ref(false);
const previewTheme = computed(() => {
  if (props.appearance === 'system') return systemDark.value ? 'dark' : 'light';
  return props.appearance;
});
const active = computed(() => intersecting.value && documentVisible.value);
const effectLabel = computed(() => slickEffectOptions.find(option => option.value === props.mode)!.label);
const layoutVersion = ref(0);
const floatingCards = new Map<number, HTMLElement>();
const date = '2026-01-01T00:00:00Z';
const slicks: Slick[] = [{ id: 1, name: 'Preview', styleName: 'presets', stylePropertiesJson: '{"presetIndex":0}',
  createdAtUtc: date, updatedAtUtc: date }];
const columns = ref<BoardColumn[]>([
  { id: 1, title: 'To do', sortKey: '1', createdAtUtc: date, updatedAtUtc: date,
    cards: [makeCard(2, 1, 'Write notes', false), makeCard(1, 1, 'Plan'),
      makeCard(4, 1, 'Send update', false)] },
  { id: 2, title: 'In progress', sortKey: '2', createdAtUtc: date, updatedAtUtc: date,
    cards: [makeCard(5, 2, 'Check details', false), makeCard(6, 2, 'Gather feedback', false),
      makeCard(10, 2, 'Prepare examples', false), makeCard(11, 2, 'Check wording', false),
      makeCard(16, 2, 'Check references', false), makeCard(3, 2, 'Review'),
      makeCard(7, 2, 'Revise draft', false), makeCard(8, 2, 'Publish', false),
      makeCard(12, 2, 'Notify team', false), makeCard(13, 2, 'File notes', false),
      makeCard(14, 2, 'Archive draft', false), makeCard(15, 2, 'Plan follow-up', false),
      makeCard(17, 2, 'Share summary', false)] }
]);
const clipping = useBoardColumnClipping(columns, layoutVersion, refreshLayout);
let scrollColumn: HTMLElement | null = null;
let frameId: number | null = null;
let elapsed = 0;
let scrollTransition: { from: number; elapsed: number } | null = null;
const liquidScrollStops = [
  { time: 0, position: 0.5 },
  { time: 800, position: 0.5 },
  { time: 1500, position: 1 },
  { time: 2000, position: 1 },
  { time: 2800, position: 0 },
  { time: 3800, position: 0 },
  { time: 4700, position: 5 / 6 },
  { time: 5100, position: 5 / 6 },
  { time: 5800, position: 0.5 },
  { time: 6500, position: 0.5 }
];
const bridgeScrollStops = [
  { time: 0, position: 0.5 },
  { time: 1000, position: 0.5 },
  { time: 2400, position: 0.63 },
  { time: 2800, position: 0.63 },
  { time: 5000, position: 0.32 },
  { time: 5600, position: 0.32 },
  { time: 7400, position: 0.5 },
  { time: 8200, position: 0.5 }
];
let observer: IntersectionObserver | null = null;
let mediaQuery: MediaQueryList | null = null;
let colourSchemeQuery: MediaQueryList | null = null;

function makeCard(id: number, boardColumnId: number, title: string, slicked = true): BoardCard {
  return { id, boardColumnId, title, slickId: slicked ? 1 : null, slick: slicked ? slicks[0]! : null, cardTypeId: 0, cardTypeName: 'Card',
    cardTypeEmoji: null, description: '', completedChecklistItemCount: 0, totalChecklistItemCount: 0,
    externalUrl: null, sortKey: String(id), tags: [], tagNames: [], cardCreatedUtc: date, cardUpdatedUtc: date };
}

function stopMotion() {
  if (frameId !== null) cancelAnimationFrame(frameId);
  frameId = null;
}

function refreshLayout() {
  layoutVersion.value++;
}

function scrollPosition(time: number) {
  const scrollStops = props.mode === 'bridges' ? bridgeScrollStops : liquidScrollStops;
  const phase = time % scrollStops[scrollStops.length - 1]!.time;
  for (let index = 1; index < scrollStops.length; index++) {
    const from = scrollStops[index - 1]!;
    const to = scrollStops[index]!;
    if (phase > to.time) continue;
    const progress = (phase - from.time) / (to.time - from.time);
    const eased = progress * progress * (3 - 2 * progress);
    return from.position + (to.position - from.position) * eased;
  }
  return scrollStops[0]!.position;
}

function startMotion() {
  stopMotion();
  const column = scrollColumn;
  if (!active.value || reducedMotion.value || !column) return;
  const distance = Math.min(432, column.scrollHeight - column.clientHeight);
  let previousTime: number | null = null;
  function scroll(timestamp: number) {
    const delta = previousTime === null ? 0 : timestamp - previousTime;
    previousTime = timestamp;
    if (scrollTransition) {
      scrollTransition.elapsed += delta;
      const progress = Math.min(1, scrollTransition.elapsed / 800);
      const eased = progress * progress * (3 - 2 * progress);
      const destination = distance * scrollPosition(0);
      column!.scrollTop = scrollTransition.from + (destination - scrollTransition.from) * eased;
      if (progress === 1) scrollTransition = null;
    } else {
      elapsed += delta;
      column!.scrollTop = distance * scrollPosition(elapsed);
    }
    frameId = requestAnimationFrame(scroll);
  }
  frameId = requestAnimationFrame(scroll);
}

function visibilityChanged() { documentVisible.value = !document.hidden; }
function motionPreferenceChanged() { reducedMotion.value = mediaQuery?.matches ?? false; }
function systemThemeChanged() { systemDark.value = colourSchemeQuery?.matches ?? false; }
watch([active, reducedMotion], startMotion, { flush: 'post' });
watch(() => props.mode, () => {
  elapsed = 0;
  if (scrollColumn) scrollTransition = { from: scrollColumn.scrollTop, elapsed: 0 };
  startMotion();
}, { flush: 'post' });
watch(previewTheme, refreshLayout, { flush: 'post' });

onMounted(() => {
  clipping.setBoardRef(board.value);
  scrollColumn = board.value!.querySelector<HTMLElement>('[data-column-id="2"]');
  scrollColumn!.scrollTop = Math.min(432, scrollColumn!.scrollHeight - scrollColumn!.clientHeight) / 2;
  mediaQuery = window.matchMedia('(prefers-reduced-motion: reduce)');
  motionPreferenceChanged();
  mediaQuery.addEventListener('change', motionPreferenceChanged);
  colourSchemeQuery = window.matchMedia('(prefers-color-scheme: dark)');
  systemThemeChanged();
  colourSchemeQuery.addEventListener('change', systemThemeChanged);
  document.addEventListener('visibilitychange', visibilityChanged);
  observer = new IntersectionObserver(entries => { intersecting.value = entries.some(entry => entry.isIntersecting); });
  observer.observe(viewport.value!);
});
onBeforeUnmount(() => {
  stopMotion();
  observer?.disconnect();
  mediaQuery?.removeEventListener('change', motionPreferenceChanged);
  colourSchemeQuery?.removeEventListener('change', systemThemeChanged);
  document.removeEventListener('visibilitychange', visibilityChanged);
});
</script>

<style scoped>
.slick-effect-preview { margin: 0; min-width: 0; }
.preview-caption { margin-bottom: 0.7rem; color: var(--bo-ink-strong); font-size: 1.1rem; font-weight: 750; }
.preview-viewport { position: relative; max-width: 100%; padding: 16px; overflow-x: auto; overflow-y: hidden; border: 1px solid var(--bo-border-soft); border-radius: 10px; background: var(--bo-surface-muted); }
.preview-scene { position: relative; width: 572px; height: 320px; margin-inline: auto; }
.preview-board { display: grid; grid-template-columns: repeat(2, 280px); gap: 12px; height: 100%; }
.column-stack { display: flex; flex-direction: column; gap: 12px; min-height: 0; position: relative; }
.column { position: relative; display: flex; flex-direction: column; min-height: 0; max-height: 100%; padding: 12px 4px; border: 1px solid var(--bo-border-soft); border-radius: 14px; background: var(--bo-surface-panel); overflow: hidden; }
.column--short { overflow: visible; }
.column--short .column-content { overflow: visible; margin-left: var(--column-scroll-gutter-left); margin-right: var(--column-scroll-gutter-right); clip-path: inset(0 0 -100vh 0); }
.preview-column-heading { position: relative; z-index: 3; margin: 0; padding: 0 8px 12px; font-size: 0.95rem; line-height: 20px; }
.column-content { position: relative; z-index: 3; display: flex; flex-direction: column; flex: 1; gap: 8px; min-width: 0; min-height: 0; overflow-y: hidden; padding: 0 8px 5px; scrollbar-width: thin; scrollbar-color: transparent transparent; scrollbar-gutter: stable both-edges; }
.column-tail-drop-zone { min-height: 8px; }
.preview-card { position: relative; z-index: 1; flex-shrink: 0; margin-bottom: 0; }
.preview-card.card--slick-gap, .preview-card.card--slick-gap-top { margin-top: 8px; }
.preview-board--liquid .preview-card.card--slick-gap { margin-top: 12px; }
.preview-card.card--slick-gap-bottom { margin-bottom: 8px; }
</style>
