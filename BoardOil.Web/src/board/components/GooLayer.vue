<template>
  <SvgGooLayer
    v-if="rendererMode === 'svg'"
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
  <span v-if="showRendererIndicator" class="goo-renderer-indicator" aria-hidden="true">{{ rendererIndicatorLabel }}</span>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { GooRenderGroup } from '../utils/gooLayout';
import HtmlGooLayer from './HtmlGooLayer.vue';
import SvgGooLayer from './SvgGooLayer.vue';

type GooRendererMode = 'html' | 'svg';
type GooRendererOverride = 'html' | 'safari' | 'svg';

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

const rendererMode = computed<GooRendererMode>(() => resolveRendererMode());
const rendererIndicatorLabel = computed(() => `g:${rendererMode.value}`);
const showRendererIndicator = computed(() => !props.layerClass.includes('goo-layer--selection'));

function resolveRendererMode(): GooRendererMode {
  const override = resolveRendererOverride();
  if (override === 'html') {
    return 'html';
  }

  if (override === 'safari' || override === 'svg') {
    return 'svg';
  }

  return isSafariBrowser() ? 'svg' : 'html';
}

function isSafariBrowser() {
  const navigatorValue = globalThis.navigator;
  const userAgent = navigatorValue?.userAgent ?? '';
  const vendor = navigatorValue?.vendor ?? '';
  const isAppleVendor = vendor.includes('Apple');
  const isSafari = userAgent.includes('Safari')
    && !userAgent.includes('Chrome')
    && !userAgent.includes('Chromium')
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

function isRendererOverrideValue(value: string | null): value is GooRendererOverride {
  return value === 'html' || value === 'safari' || value === 'svg';
}
</script>

<style scoped>
.goo-renderer-indicator {
  position: absolute;
  right: 0.35rem;
  bottom: 0.25rem;
  z-index: 5;
  pointer-events: none;
  font-size: 0.625rem;
  line-height: 1;
  color: color-mix(in srgb, var(--bo-ink-muted) 72%, transparent);
  background: color-mix(in srgb, var(--bo-surface-panel) 72%, transparent);
  border: 1px solid color-mix(in srgb, var(--bo-border-soft) 58%, transparent);
  border-radius: 4px;
  padding: 0.12rem 0.22rem;
}
</style>
