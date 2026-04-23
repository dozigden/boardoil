<template>
  <ModalDialog :open="open" title="About BoardOil" close-label="Close About" @close="emit('close')">
    <section class="about-dialog-body">
      <section class="about-summary-grid" aria-label="Build summary">
        <p class="about-summary-line">
          <template v-for="(item, index) in summaryItems" :key="item.label">
            <span :class="['about-summary-item', { 'about-summary-item--mismatch': showSeparateBySide }]">
              <strong>{{ item.label }}</strong>
              <span>{{ formatBuildInfo(item.buildInfo) }}</span>
            </span>
            <span v-if="index < summaryItems.length - 1" class="about-summary-separator" aria-hidden="true">|</span>
          </template>
        </p>
        <p class="about-dialog-format">version (channel/build) commit</p>
        <p v-if="loadingBackendBuildInfo" class="about-metadata-state">Loading backend metadata...</p>
        <p v-else-if="backendBuildInfoLoadError" class="about-metadata-state about-metadata-state--error">{{ backendBuildInfoLoadError }}</p>
      </section>
    </section>
  </ModalDialog>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import type { BuildInfo } from '../../shared/api/versionApi';
import { getBackendBuildInfo, getFrontendBuildInfo } from '../../shared/api/versionApi';
import { areBuildMetadataInSync, formatBuildInfo, getAboutSummaryItems } from './aboutDialogMetadata';
import ModalDialog from '../../shared/components/ModalDialog.vue';

const props = defineProps<{
  open: boolean;
}>();

const emit = defineEmits<{
  close: [];
}>();

const frontendBuildInfo = getFrontendBuildInfo();
const backendBuildInfo = ref<BuildInfo | null>(null);
const loadingBackendBuildInfo = ref(false);
const backendBuildInfoLoadError = ref<string | null>(null);
const buildMetadataInSync = computed(() => areBuildMetadataInSync(frontendBuildInfo, backendBuildInfo.value));
const showSeparateBySide = computed(() => buildMetadataInSync.value === false);
const summaryItems = computed(() => getAboutSummaryItems(frontendBuildInfo, backendBuildInfo.value, buildMetadataInSync.value));

watch(
  () => props.open,
  async isOpen => {
    if (!isOpen || backendBuildInfo.value || loadingBackendBuildInfo.value) {
      return;
    }

    loadingBackendBuildInfo.value = true;
    backendBuildInfoLoadError.value = null;

    const result = await getBackendBuildInfo();
    if (result.ok) {
      backendBuildInfo.value = result.data;
    } else {
      backendBuildInfoLoadError.value = result.error.message;
    }

    loadingBackendBuildInfo.value = false;
  },
  { immediate: true }
);
</script>

<style scoped>
.about-dialog-body {
  display: grid;
  gap: 0.6rem;
}

.about-summary-grid {
  --about-summary-label-width: 5.5rem;
  --about-summary-col-gap: 0.5rem;
  display: grid;
  gap: 0.25rem;
}

.about-summary-line {
  margin: 0;
  display: flex;
  align-items: baseline;
  flex-wrap: wrap;
  gap: 0.4rem;
  color: var(--bo-ink-default);
}

.about-summary-item {
  display: inline-grid;
  grid-template-columns: var(--about-summary-label-width) auto;
  column-gap: var(--about-summary-col-gap);
  align-items: baseline;
}

.about-summary-item strong {
  color: var(--bo-ink-strong);
}

.about-summary-item--mismatch,
.about-summary-item--mismatch strong {
  color: var(--bo-colour-danger-ink);
}

.about-summary-separator {
  color: var(--bo-ink-muted);
  margin: 0 0.1rem 0 0.2rem;
}

.about-metadata-state {
  margin: 0;
  color: var(--bo-ink-default);
}

.about-metadata-state--error {
  color: var(--bo-colour-danger-ink);
}

.about-dialog-format {
  margin: 0;
  padding-inline-start: calc(var(--about-summary-label-width) + var(--about-summary-col-gap));
  color: var(--bo-ink-muted);
  font-size: 0.82rem;
  text-transform: none;
}
</style>
