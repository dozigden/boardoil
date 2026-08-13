import { afterEach, describe, expect, it, vi } from 'vitest';
import { resolveGooRendererMode } from './gooRendererMode';

describe('resolveGooRendererMode', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('uses the full renderer by default', () => {
    stubBrowser('Mozilla/5.0 Chrome/140.0.0.0 Safari/537.36', 'Google Inc.', '');

    expect(resolveGooRendererMode()).toBe('full');
  });

  it('uses the lite renderer for Safari', () => {
    stubBrowser(
      'Mozilla/5.0 (Macintosh; Intel Mac OS X) AppleWebKit/605.1.15 Version/18.6 Safari/605.1.15',
      'Apple Computer, Inc.',
      ''
    );

    expect(resolveGooRendererMode()).toBe('lite');
  });

  it('honours temporary query-string renderer overrides', () => {
    stubBrowser('Mozilla/5.0 Chrome/140.0.0.0 Safari/537.36', 'Google Inc.', '?gooRenderer=lite');

    expect(resolveGooRendererMode()).toBe('lite');
  });

  it('does not read browser storage', () => {
    const storageGetItem = vi.fn(() => 'lite');
    vi.stubGlobal('localStorage', { getItem: storageGetItem });
    stubBrowser('Mozilla/5.0 Chrome/140.0.0.0 Safari/537.36', 'Google Inc.', '');

    expect(resolveGooRendererMode()).toBe('full');
    expect(storageGetItem).not.toHaveBeenCalled();
  });
});

function stubBrowser(userAgent: string, vendor: string, search: string) {
  vi.stubGlobal('navigator', { userAgent, vendor });
  vi.stubGlobal('window', { location: { search } });
}
