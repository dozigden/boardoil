import { describe, expect, it } from 'vitest';
import { syncPlainTextAreaHeight } from './mdEditorPlainTextSizing';

describe('syncPlainTextAreaHeight', () => {
  it('sets textarea height to its scrollHeight in pixels', () => {
    const textarea = {
      style: { height: '40px' },
      scrollHeight: 128
    } as unknown as HTMLTextAreaElement;

    syncPlainTextAreaHeight(textarea);

    expect(textarea.style.height).toBe('128px');
  });

  it('does nothing for null textarea', () => {
    expect(() => syncPlainTextAreaHeight(null)).not.toThrow();
  });
});
