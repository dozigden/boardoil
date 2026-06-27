<template>
  <SvgGooLayer
    v-if="useSvgGooLayer"
    :groups="groups"
    :blob-border-radius-px="blobBorderRadiusPx"
    :layer-class="layerClass"
    :group-class="groupClass"
    :blob-class="blobClass"
  />
  <HtmlGooLayer
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
import HtmlGooLayer from './HtmlGooLayer.vue';
import SvgGooLayer from './SvgGooLayer.vue';

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

const useSvgGooLayer = computed(() => resolveUseSvgGooLayer());

function resolveUseSvgGooLayer() {
  const override = resolveRendererOverride();
  if (override !== null) {
    return override === 'svg' || override === 'safari';
  }

  const navigatorValue = globalThis.navigator;
  const userAgent = navigatorValue?.userAgent ?? '';
  const vendor = navigatorValue?.vendor ?? '';
  const isAppleVendor = vendor.includes('Apple');
  const isSafari = userAgent.includes('Safari')
    && !userAgent.includes('Chrome')
    && !userAgent.includes('Chromium')
    && !userAgent.includes('CriOS')
    && !userAgent.includes('FxiOS')
    && !userAgent.includes('Edg/');

  return isAppleVendor && isSafari;
}

function resolveRendererOverride() {
  const searchOverride = resolveRendererOverrideFromSearch();
  if (searchOverride !== null) {
    return searchOverride;
  }

  try {
    const value = globalThis.localStorage?.getItem('boardoil:goo-renderer')?.trim().toLowerCase() ?? null;
    return isRendererOverrideValue(value) ? value : null;
  } catch {
    return null;
  }
}

function resolveRendererOverrideFromSearch() {
  const search = typeof window !== 'undefined' ? window.location?.search ?? '' : '';
  if (!search) {
    return null;
  }

  const value = new URLSearchParams(search).get('gooRenderer')?.trim().toLowerCase() ?? null;
  return isRendererOverrideValue(value) ? value : null;
}

function isRendererOverrideValue(value: string | null): value is 'html' | 'safari' | 'svg' {
  return value === 'html' || value === 'safari' || value === 'svg';
}
</script>
