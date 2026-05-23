<template>
  <div v-if="groups.length > 0" :class="['goo-layer', layerClass]" aria-hidden="true">
    <div
      v-for="group in groups"
      :key="group.id"
      :class="['goo-group', groupClass]"
      :style="{
        '--goo-colour': group.colour,
        '--goo-radius': `${blobBorderRadiusPx}px`
      }"
    >
      <span
        v-for="blob in group.blobs"
        :key="blob.id"
        :class="['goo-blob', blobClass]"
        :style="{
          top: `${blob.top}px`,
          left: `${blob.left}px`,
          width: `${blob.width}px`,
          height: `${blob.height}px`,
          clipPath: blob.clipPath,
          borderRadius: blob.borderRadius
        }"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import type { GooRenderGroup } from '../utils/gooLayout';

withDefaults(defineProps<{
  groups: GooRenderGroup[];
  blobBorderRadiusPx: number;
  layerClass?: string;
  groupClass?: string;
  blobClass?: string;
}>(), {
  layerClass: '',
  groupClass: '',
  blobClass: ''
});
</script>

<style scoped>
.goo-layer {
  position: absolute;
  inset: 0;
  pointer-events: none;
  z-index: 2;
  overflow: visible;
}

.goo-group {
  position: absolute;
  inset: 0;
  filter: url(#goo);
}

.goo-blob {
  position: absolute;
  border-radius: var(--goo-radius);
  background: var(--goo-colour);
  transform: translateY(-50%);
}

.goo-layer--selection {
  z-index: 4;
  animation: goo-selection-pulse 1.5s ease-in-out infinite alternate;
}

.goo-blob--selection {
  background: var(--goo-colour);
}

@keyframes goo-selection-pulse {
  from {
    opacity: 0.2;
  }

  to {
    opacity: 0.38;
  }
}

@media (prefers-reduced-motion: reduce) {
  .goo-layer--selection {
    animation: none;
    opacity: 0.7;
  }
}
</style>
