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
import type { GooRenderGroup } from '../utils/gooLayout';
import type { GooRendererMode } from '../utils/gooRendererMode';
import FullGooLayer from './FullGooLayer.vue';
import LiteGooLayer from './LiteGooLayer.vue';

withDefaults(defineProps<{
  gooRendererMode: GooRendererMode;
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
