<template>
  <svg ref="icon" class="splash-icon" :class="{ arriving }" width="32" height="32" viewBox="0 0 48 48"
    aria-hidden="true" focusable="false">
    <g fill="#754881" stroke="#382439" stroke-width="1.25" stroke-linejoin="round">
      <g v-if="arriving" class="source">
        <path class="neck" d="M10 -2 H34 C32 0 27 1 25.4 5 C24.2 8 24.5 12 22 14
          C19.5 12 19.8 8 18.6 5 C17 1 12 0 10 -2Z" />
      </g>
      <g class="body">
        <path :d="splash">
          <animate v-if="arriving" ref="spread" attributeName="d" begin="indefinite" :dur="duration"
            :values="[bead, bead, splash, splash].join(';')" keyTimes="0;0.66;0.97;1"
            calcMode="spline" keySplines="0 0 1 1; .32 .04 .28 1; 0 0 1 1" />
        </path>
      </g>
      <g class="fleck">
        <ellipse cx="38.1" cy="8.7" rx="2.1" ry="1.4" transform="rotate(-27 38.1 8.7)" />
      </g>
    </g>
    <g v-if="!plain" class="mark" fill="#fff">
      <path d="M19.7 20.4 C19.7 17.9 24.3 17.9 24.3 20.4 L23.5 25.9 C23.3 27.4 20.7 27.4 20.5 25.9Z" />
      <circle cx="22" cy="30.2" r="1.85" />
    </g>
  </svg>
</template>

<script lang="ts">
// Header layouts remount on navigation. Only a new document replays the entrance.
let entrancePlayed = false;
</script>

<script setup lang="ts">
import { nextTick, onBeforeUnmount, onMounted, ref, useTemplateRef } from 'vue';

const { plain = false } = defineProps<{ plain?: boolean }>();

// Both outlines have corresponding clockwise curves, so their perimeter moves continuously.
const bead = `M22 13
  C22.53 13 23.05 13.15 23.59 13.45
  C24.16 13.76 24.76 14.25 25.37 14.88
  C25.98 15.52 26.61 16.3 27.25 17.21
  C27.93 18.18 28.63 19.29 29.3 20.5
  C29.96 21.71 30.6 23.02 31.16 24.37
  C31.65 25.56 32.08 26.78 32.4 28
  C32.77 29.4 33 30.79 33.04 32.13
  C33.09 33.39 32.96 34.6 32.66 35.73
  C32.35 36.85 31.86 37.88 31.2 38.79
  C30.58 39.64 29.81 40.38 28.91 40.99
  C27.95 41.64 26.86 42.15 25.68 42.49
  C24.11 42.94 22.41 43.1 20.75 42.94
  C19.42 42.82 18.13 42.5 16.95 42
  C15.85 41.53 14.85 40.9 14.01 40.14
  C13.17 39.37 12.48 38.46 11.98 37.44
  C11.52 36.49 11.21 35.45 11.06 34.34
  C10.88 32.99 10.92 31.56 11.16 30.09
  C11.43 28.36 11.96 26.59 12.63 24.88
  C13.37 23 14.28 21.2 15.21 19.61
  C16.31 17.73 17.45 16.14 18.51 15.01
  C19.21 14.27 19.88 13.72 20.53 13.38
  Q21.28 13 22 13Z`;
const splash = `M22 4       C25.1 3.2 27 5.1 26.7 8.1       C26.6 10.6 26.1 12.1 28.2 12.5       C29.6 12.9 31.2 10.3 33.5 10.9       C36.3 13.1 36.6 16.3 34.2 18.1       C32.7 19.2 30 20.3 30.8 22       C31.6 23.8 37.2 20.8 40.3 21.7       C43.3 22.5 44.7 25.7 43 28       C40.2 31.8 31.8 28.7 30.1 30.5       C29.2 32.3 35.2 35.2 37 38.1       C39.2 41.1 36.4 44 33.3 42.5       C30 41.3 28.4 37.7 25 38.2       C22.1 38.6 22.5 42.9 18.9 43.1       C15.9 43.3 14.7 40.8 15.5 38       C15.9 35.8 14.9 34.3 12.5 34.5       C10 34.7 7.2 36.5 4.8 34.7       C1.9 33.6 2.2 29.7 5.1 27.9       C8 26 11.9 25.7 12.1 22.9       C12.2 20.5 7.8 19.4 7.3 16.8       C6.3 13.5 8.4 11 11.2 11.4       C13.7 11.5 14.1 14.5 16.4 13.7       C18.6 12.9 16.7 7.2 19.4 5.4       Q20.3 4.4 22 4Z`;
const duration = '2.8s';
const icon = useTemplateRef<SVGSVGElement>('icon');
const spread = useTemplateRef<SVGAnimateElement>('spread');
const arriving = ref(false);
let motion: MediaQueryList | null = null;

function settle() {
  arriving.value = false;
  motion?.removeEventListener('change', settle);
}

onMounted(async () => {
  if (plain || entrancePlayed || !icon.value) return;
  entrancePlayed = true;
  motion = window.matchMedia('(prefers-reduced-motion: reduce)');
  if (motion.matches) return;

  const bounds = icon.value.getBoundingClientRect();
  const svgUnitsPerPixel = icon.value.viewBox.baseVal.height / bounds.height;
  icon.value.style.setProperty('--viewport-edge', `${-bounds.top * svgUnitsPerPixel}px`);
  motion.addEventListener('change', settle);
  arriving.value = true;
  await nextTick();
  spread.value?.addEventListener('endEvent', settle, { once: true });
  spread.value?.beginElement();
});

onBeforeUnmount(() => motion?.removeEventListener('change', settle));
</script>

<style scoped>
.splash-icon { display: block; overflow: visible; flex: none; pointer-events: none; }
.source { transform: translate(2px, var(--viewport-edge)); }
.neck { transform-origin: 22px 0; }
.body { transform-origin: 22px 24px; }
.fleck { transform-origin: 38px 9px; }

.arriving .neck { animation: pinch v-bind(duration) both; }
.arriving .body { animation: land v-bind(duration) both; }
.arriving .fleck { animation: flick v-bind(duration) both; }
.arriving .mark { animation: reveal v-bind(duration) both; }

@keyframes pinch {
  0% { transform: scale(.25, 0); }
  30% { transform: scale(1, .64); }
  47% { transform: scale(.85, .83); }
  55% { transform: scale(.47, 1); }
  59% { transform: scale(.25, .7); }
  64%, 100% { transform: scale(.4, 0); }
}

/* The bead lands at (24, 34), along the lower edge of the splash's central body. */
@keyframes land {
  0% { transform: translate(2px, calc(var(--viewport-edge) - 25px)) scale(.2, .1); }
  32% { transform: translate(2px, calc(var(--viewport-edge) - 20.5px)) scale(.78, .71); }
  49% { transform: translate(2px, calc(var(--viewport-edge) - 18px)) scale(.95, .9); }
  56% { transform: translate(2px, calc(var(--viewport-edge) - 15.4px)) scale(.88, 1.02); animation-timing-function: cubic-bezier(.52, .04, .86, .56); }
  66% { transform: translate(2px, -9px) scale(.9, 1); animation-timing-function: ease-out; }
  73% { transform: translate(2px, .5px) scale(1.04, .76); }
  81% { transform: translate(1px, 1px) scale(1.03, .86); }
  97%, 100% { transform: scale(1); }
}
@keyframes flick {
  0%, 79% { opacity: 0; transform: translate(-7px, 8px) scale(.25); }
  84% { opacity: 1; }
  95%, 100% { opacity: 1; transform: none; }
}
@keyframes reveal {
  0%, 85% { opacity: 0; }
  100% { opacity: 1; }
}
</style>
