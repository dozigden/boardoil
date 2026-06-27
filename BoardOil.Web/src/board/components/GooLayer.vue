<template>
  <LiteGooLayer
    v-if="gooRendererMode === 'lite'"
    :groups="groups"
    :blob-border-radius-px="blobBorderRadiusPx"
    :layer-class="layerClass"
    :group-class="groupClass"
    :blob-class="blobClass"
  />
  <FullGooLayer
    v-else
    :groups="groups"
    :blob-border-radius-px="blobBorderRadiusPx"
    :layer-class="layerClass"
    :group-class="groupClass"
    :blob-class="blobClass"
  />
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { GooRenderGroup } from '../utils/gooLayout';
import { resolveGooRendererMode, type GooRendererMode } from '../utils/gooRendererMode';
import FullGooLayer from './FullGooLayer.vue';
import LiteGooLayer from './LiteGooLayer.vue';

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

const gooRendererMode = computed<GooRendererMode>(() => resolveGooRendererMode());
</script>
