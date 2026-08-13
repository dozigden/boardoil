import { readBrowserStorageItem } from '../../shared/utils/browserStorage';

export type GooRendererMode = 'full' | 'lite';
type GooRendererOverride = GooRendererMode | 'html' | 'safari' | 'svg';

export function resolveGooRendererMode(): GooRendererMode {
  const override = resolveRendererOverride();
  if (override === 'full' || override === 'html') {
    return 'full';
  }

  if (override === 'lite' || override === 'safari' || override === 'svg') {
    return 'lite';
  }

  if (isSafariBrowser()) {
    return 'lite';
  }

  return 'full';
}

function isSafariBrowser() {
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

  const value = readBrowserStorageItem('boardoil:goo-renderer')?.trim().toLowerCase() ?? null;
  return isRendererOverrideValue(value) ? value : null;
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
  return value === 'full'
    || value === 'html'
    || value === 'lite'
    || value === 'safari'
    || value === 'svg';
}
