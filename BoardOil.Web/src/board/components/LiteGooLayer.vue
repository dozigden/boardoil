<template>
  <svg v-if="liteGroups.length > 0" :class="['goo-layer', 'goo-layer--lite', layerClass]" aria-hidden="true" focusable="false">
    <defs>
      <template v-for="group in liteGroups" :key="`${group.id}-clips`">
        <clipPath
          v-for="blob in group.blobs.filter(hasClipInsets)"
          :id="getBlobClipId(group.id, blob.id)"
          :key="getBlobClipId(group.id, blob.id)"
          clipPathUnits="userSpaceOnUse"
        >
          <rect
            :x="getClipRectX(blob)"
            :y="getClipRectY(blob)"
            :width="getClipRectWidth(blob)"
            :height="getClipRectHeight(blob)"
          />
        </clipPath>
      </template>
    </defs>

    <g
      v-for="group in liteGroups"
      :key="group.id"
      :class="['goo-group', groupClass]"
      :style="{
        '--goo-colour': group.colour,
        '--goo-radius': `${blobBorderRadiusPx}px`
      }"
      filter="url(#goo)"
    >
      <rect
        v-for="blob in group.blobs"
        :key="blob.id"
        :class="['goo-blob', blobClass]"
        :x="blob.left"
        :y="getBlobY(blob)"
        :width="blob.width"
        :height="blob.height"
        :rx="blobBorderRadiusPx"
        :ry="blobBorderRadiusPx"
        :clip-path="blob.clipInsets ? `url(#${getBlobClipId(group.id, blob.id)})` : undefined"
      />
    </g>
  </svg>
</template>

<script setup lang="ts">
import { computed, useId } from 'vue';
import type { GooRenderBlob, GooRenderGroup } from '../utils/gooLayout';

const props = withDefaults(defineProps<{
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

const clipIdPrefix = sanitizeSvgIdPart(`goo-clip-${useId()}`);
const liteGroups = computed(() =>
  props.groups
    .map(group => ({
      ...group,
      blobs: group.blobs.filter(blob => !isBridgeBlob(blob))
    }))
    .filter(group => group.blobs.length > 0)
);

function isBridgeBlob(blob: GooRenderBlob) {
  return blob.id.includes('-bridge-');
}

function hasClipInsets(blob: GooRenderBlob) {
  return blob.clipInsets !== undefined;
}

function getBlobClipId(groupId: string, blobId: string) {
  return `${clipIdPrefix}-${sanitizeSvgIdPart(groupId)}-${sanitizeSvgIdPart(blobId)}`;
}

function getBlobY(blob: GooRenderBlob) {
  return blob.top - (blob.height / 2);
}

function getClipRectX(blob: GooRenderBlob) {
  return blob.left + (blob.clipInsets?.left ?? 0);
}

function getClipRectY(blob: GooRenderBlob) {
  return getBlobY(blob) + (blob.clipInsets?.top ?? 0);
}

function getClipRectWidth(blob: GooRenderBlob) {
  const insets = blob.clipInsets;
  if (!insets) {
    return blob.width;
  }

  return Math.max(0, blob.width - insets.left - insets.right);
}

function getClipRectHeight(blob: GooRenderBlob) {
  const insets = blob.clipInsets;
  if (!insets) {
    return blob.height;
  }

  return Math.max(0, blob.height - insets.top - insets.bottom);
}

function sanitizeSvgIdPart(value: string) {
  return value.replace(/[^A-Za-z0-9_-]/g, '-');
}
</script>

<style scoped>
.goo-layer {
  position: absolute;
  inset: 0;
  pointer-events: none;
  z-index: 2;
  overflow: visible;
  width: 100%;
  height: 100%;
}

.goo-group {
  overflow: visible;
}

.goo-blob {
  fill: var(--goo-colour);
}

.goo-layer--selection {
  z-index: 4;
  animation: goo-selection-pulse 1.5s ease-in-out infinite alternate;
}

.goo-layer--column {
  z-index: 0;
}

.goo-layer--column.goo-layer--selection {
  z-index: 0;
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
