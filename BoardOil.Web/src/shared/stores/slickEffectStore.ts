import { ref } from 'vue';
import { defineStore } from 'pinia';
import { isBrowserStorageEnabled, readBrowserStorageItem, writeBrowserStorageItem } from '../utils/browserStorage';
import type { SlickEffect } from '../types/slickEffect';

const STORAGE_KEY = 'boardoil:slick-effect';

export const useSlickEffectStore = defineStore('slickEffect', () => {
  const effect = ref<SlickEffect>('bridges');
  let initialized = false;

  function SET_EFFECT(value: SlickEffect) {
    effect.value = value;
  }

  function initialize() {
    if (initialized) return;
    SET_EFFECT(readEffect(readBrowserStorageItem(STORAGE_KEY)));
    if (isBrowserStorageEnabled()) globalThis.window?.addEventListener('storage', onStorage);
    initialized = true;
  }

  function setEffect(value: SlickEffect) {
    SET_EFFECT(value);
    writeBrowserStorageItem(STORAGE_KEY, value);
  }

  function onStorage(event: StorageEvent) {
    if (event.key !== STORAGE_KEY && event.key !== null) return;
    try {
      if (event.storageArea !== globalThis.localStorage) return;
    } catch {
      return;
    }
    SET_EFFECT(readEffect(event.newValue));
  }

  function dispose() {
    globalThis.window?.removeEventListener('storage', onStorage);
    initialized = false;
  }

  return { effect, initialize, setEffect, dispose };
});

function readEffect(value: string | null): SlickEffect {
  if (value === 'none' || value === 'bridges' || value === 'liquid') return value;
  return 'bridges';
}
