<template>
  <button v-if="hasAnnouncement" ref="trigger" type="button" class="btn announcement-trigger"
    :class="{ 'announcement-trigger--pending': !showTrigger }"
    aria-haspopup="dialog" aria-label="What’s new?" title="What’s new?" @click="open = true">
    <AnnouncementSplash v-if="showTrigger" />
  </button>
  <FixedChromeDialog v-if="open" open class="announcement-dialog" title="What’s New?" close-label="Close announcement"
    @opened="markRead" @close="closeAnnouncement">
    <template #title>
      <span class="announcement-title"><AnnouncementSplash plain />What’s New?</span>
    </template>
    <div class="announcement-content">
      <p>Control how cards in the same slick look, classic Bridges or new Liquid effect.</p>
      <div class="announcement-effect">
        <h4>Slick effect</h4>
        <SlickEffectPicker />
      </div>
      <SlickEffectPreview :mode="slickEffect.effect" :appearance="theme.mode" />
      <p>You can change this later in <strong>User settings → Theme</strong>.</p>
      <p v-if="readError" class="error" role="alert">{{ readError }}</p>
    </div>
    <template #actions>
      <div class="fixed-chrome-dialog-actions fixed-chrome-dialog-actions--end">
        <button type="button" class="btn" @click="closeAnnouncement">Close</button>
      </div>
    </template>
  </FixedChromeDialog>
</template>

<script setup lang="ts">
import { computed, defineAsyncComponent, nextTick, ref, useTemplateRef, watch, watchPostEffect } from 'vue';
import FixedChromeDialog from '../../shared/components/FixedChromeDialog.vue';
import { useAuthStore } from '../../shared/stores/authStore';
import { useSlickEffectStore } from '../../shared/stores/slickEffectStore';
import { useThemeStore } from '../../shared/stores/themeStore';
import AnnouncementSplash from './AnnouncementSplash.vue';
import SlickEffectPicker from './SlickEffectPicker.vue';

const SlickEffectPreview = defineAsyncComponent(() => import('./SlickEffectPreview.vue'));
const props = defineProps<{ pageReady: boolean }>();
const emit = defineEmits<{ focusUserMenu: [] }>();
const previewAnnouncement = import.meta.env.DEV && import.meta.env.VITE_BO_PREVIEW_ANNOUNCEMENT !== 'false';
const auth = useAuthStore();
const hasAnnouncement = computed(() => previewAnnouncement || auth.user?.hasUnreadAnnouncement);
const slickEffect = useSlickEffectStore();
const theme = useThemeStore();
const open = ref(false);
const readError = ref('');
const showTrigger = ref(false);
const trigger = useTemplateRef<HTMLButtonElement>('trigger');

async function closeAnnouncement() {
  open.value = false;
  await nextTick();
  if (trigger.value) trigger.value.focus({ preventScroll: true });
  else emit('focusUserMenu');
}

// A slow acknowledgement can remove the trigger after the dialog has returned focus to it.
watch(hasAnnouncement, visible => {
  if (!visible && trigger.value === document.activeElement) emit('focusUserMenu');
});

watchPostEffect(onCleanup => {
  if (showTrigger.value || !props.pageReady || !hasAnnouncement.value) return;

  let cancelled = false;
  let frame = 0;
  async function afterLoad() {
    await document.fonts.ready;
    if (cancelled) return;
    // Give the page a paint with its controls in place before mounting the animated icon.
    frame = requestAnimationFrame(() => {
      frame = requestAnimationFrame(() => { showTrigger.value = true; });
    });
  }

  if (document.readyState === 'complete') void afterLoad();
  else window.addEventListener('load', afterLoad, { once: true });
  onCleanup(() => {
    cancelled = true;
    window.removeEventListener('load', afterLoad);
    cancelAnimationFrame(frame);
  });
});

async function markRead() {
  if (previewAnnouncement) return;
  readError.value = '';
  const result = await auth.markAnnouncementRead();
  if (!result.ok) readError.value = 'Couldn’t mark this announcement as read. Open it again to retry.';
}
</script>

<style scoped>
.announcement-trigger {
  --bo-btn-bg: transparent;
  --bo-btn-border: transparent;
  --bo-btn-bg-hover: var(--bo-surface-muted);
  --bo-btn-border-hover: var(--bo-border-brand);
  width: 2.25rem;
  height: 2.25rem;
  padding: 0;
  flex: none;
}
.announcement-dialog { width: min(42rem, calc(100vw - 2rem)); }
.announcement-title { display: flex; align-items: center; gap: 0.5rem; }
.announcement-effect { display: grid; gap: 0.5rem; }
.announcement-trigger--pending { visibility: hidden; }
.announcement-content { display: grid; grid-template-columns: minmax(0, 1fr); min-width: 0; gap: 1rem; }
.announcement-content h4, .announcement-content p { margin: 0; }

</style>
