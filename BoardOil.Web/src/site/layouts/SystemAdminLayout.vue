<template>
  <section class="app-layout app-layout--admin app-layout-with-header">
    <AppHeader />
    <p v-if="errorMessage" class="error">{{ errorMessage }}</p>
    <section class="app-layout-admin-content">
      <slot />
    </section>
  </section>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { onMounted, onUnmounted } from 'vue';
import AppHeader from '../components/AppHeader.vue';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';

const feedbackStore = useUiFeedbackStore();
const { errorMessage } = storeToRefs(feedbackStore);

type InlineStyleSnapshot = {
  element: HTMLElement;
  properties: Record<string, { value: string; priority: string }>;
};

const PAGE_LOCK_PROPERTIES = [
  'overflow',
  'height',
  'overscroll-behavior',
  'position',
  'inset',
  'width'
] as const;

const APP_LOCK_PROPERTIES = ['overflow', 'height'] as const;

const styleSnapshots: InlineStyleSnapshot[] = [];
let lockedScrollY = 0;

onMounted(() => {
  lockedScrollY = window.scrollY;
  captureAndApplyStyles(document.documentElement, PAGE_LOCK_PROPERTIES, {
    overflow: 'hidden',
    height: '100%',
    'overscroll-behavior': 'none',
    position: 'fixed',
    inset: '0',
    width: '100%'
  });
  captureAndApplyStyles(document.body, PAGE_LOCK_PROPERTIES, {
    overflow: 'hidden',
    height: '100%',
    'overscroll-behavior': 'none',
    position: 'fixed',
    inset: '0',
    width: '100%'
  });

  const appRoot = document.getElementById('app');
  if (appRoot) {
    captureAndApplyStyles(appRoot, APP_LOCK_PROPERTIES, {
      overflow: 'hidden',
      height: '100%'
    });
  }
});

onUnmounted(() => {
  for (const snapshot of styleSnapshots.reverse()) {
    for (const [propertyName, state] of Object.entries(snapshot.properties)) {
      if (state.value.length === 0) {
        snapshot.element.style.removeProperty(propertyName);
        continue;
      }

      snapshot.element.style.setProperty(propertyName, state.value, state.priority);
    }
  }

  styleSnapshots.length = 0;
  window.scrollTo(0, lockedScrollY);
});

function captureAndApplyStyles(
  element: HTMLElement,
  properties: readonly string[],
  nextValues: Record<string, string>
) {
  const snapshot: InlineStyleSnapshot = {
    element,
    properties: {}
  };

  for (const propertyName of properties) {
    snapshot.properties[propertyName] = {
      value: element.style.getPropertyValue(propertyName),
      priority: element.style.getPropertyPriority(propertyName)
    };

    const nextValue = nextValues[propertyName] ?? '';
    if (nextValue.length === 0) {
      element.style.removeProperty(propertyName);
      continue;
    }

    element.style.setProperty(propertyName, nextValue, 'important');
  }

  styleSnapshots.push(snapshot);
}
</script>

<style scoped>
.app-layout--admin {
  position: fixed;
  inset: 0;
  height: 100vh;
  height: 100dvh;
  min-height: 100vh;
  min-height: 100dvh;
  display: flex;
  flex-direction: column;
  min-width: 0;
  overflow: hidden;
  --bo-header-margin-active: 0;
  --bo-admin-scroll-inline-padding: 1rem;
  --bo-admin-scroll-block-start-padding: 1rem;
  --bo-admin-scroll-block-end-padding: 1rem;
}

.app-layout-admin-content {
  flex: 1;
  height: 0;
  min-height: 0;
  min-width: 0;
  display: flex;
  flex-direction: column;
  padding: 0;
  overflow: hidden;
  position: relative;
}

.app-layout-admin-content :deep(.admin-content > *) {
  margin-top: 0;
  max-width: none;
}

@media (max-width: 720px) {
  .app-layout--admin {
    --bo-admin-scroll-inline-padding: 0.75rem;
    --bo-admin-scroll-block-start-padding: 0.75rem;
    --bo-admin-scroll-block-end-padding: 0.75rem;
  }
}
</style>
