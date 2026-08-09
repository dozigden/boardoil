import { afterEach, describe, expect, it, vi } from 'vitest';
import { copyTextToClipboard } from './clipboard';

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe('copyTextToClipboard', () => {
  it('uses the Clipboard API when it is available', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    const { execCommand } = installClipboardDom();
    vi.stubGlobal('navigator', { clipboard: { writeText } });

    const copied = await copyTextToClipboard('error details');

    expect(copied).toBe(true);
    expect(writeText).toHaveBeenCalledWith('error details');
    expect(execCommand).not.toHaveBeenCalled();
  });

  it('falls back to a selected textarea when the Clipboard API is unavailable', async () => {
    const { document, execCommand } = installClipboardDom(true);
    vi.stubGlobal('navigator', {});

    const copied = await copyTextToClipboard('error details');

    expect(copied).toBe(true);
    expect(execCommand).toHaveBeenCalledWith('copy');
    expect(document.querySelector('textarea')).toBeNull();
  });

  it('keeps the fallback copy source inside the active modal dialog', async () => {
    const { document, execCommand } = installClipboardDom(true, true);
    const dialog = document.querySelector('dialog');
    const copyButton = document.querySelector('button');
    expect(dialog).not.toBeNull();
    expect(copyButton).not.toBeNull();
    copyButton!.focus();
    execCommand.mockImplementation(() => {
      const textArea = dialog!.querySelector('textarea');
      expect(textArea).not.toBeNull();
      expect(document.activeElement).toBe(textArea);
      return true;
    });
    vi.stubGlobal('navigator', {});

    const copied = await copyTextToClipboard('error details');

    expect(copied).toBe(true);
    expect(execCommand).toHaveBeenCalledWith('copy');
    expect(dialog!.querySelector('textarea')).toBeNull();
    expect(document.activeElement).toBe(copyButton);
  });

  it('falls back when the Clipboard API denies access', async () => {
    const writeText = vi.fn().mockRejectedValue(new Error('Clipboard access denied'));
    const { execCommand } = installClipboardDom(true);
    vi.stubGlobal('navigator', { clipboard: { writeText } });

    const copied = await copyTextToClipboard('error details');

    expect(copied).toBe(true);
    expect(execCommand).toHaveBeenCalledWith('copy');
  });

  it('reports failure when neither copy mechanism succeeds', async () => {
    const { document } = installClipboardDom(false);
    vi.stubGlobal('navigator', {});

    const copied = await copyTextToClipboard('error details');

    expect(copied).toBe(false);
    expect(document.querySelector('textarea')).toBeNull();
  });
});

function installClipboardDom(copyResult = true, withOpenDialog = false) {
  const execCommand = vi.fn(() => copyResult);
  const document = new FakeDocument(execCommand);
  vi.stubGlobal('HTMLElement', FakeHTMLElement);
  vi.stubGlobal('document', document as unknown as Document);

  if (withOpenDialog) {
    const dialog = document.createElement('dialog');
    dialog.open = true;
    const copyButton = document.createElement('button');
    dialog.appendChild(copyButton);
    document.body.appendChild(dialog);
  }

  return { document, execCommand };
}

class FakeDocument {
  readonly body: FakeHTMLElement;
  activeElement: FakeHTMLElement | null = null;

  constructor(readonly execCommand: ReturnType<typeof vi.fn>) {
    this.body = new FakeHTMLElement(this, 'body');
  }

  createElement(tagName: string): FakeHTMLElement {
    return new FakeHTMLElement(this, tagName);
  }

  querySelector(selector: string): FakeHTMLElement | null {
    return this.body.querySelector(selector);
  }
}

class FakeHTMLElement {
  readonly style: Record<string, string> = {};
  readonly children: FakeHTMLElement[] = [];
  value = '';
  readOnly = false;
  tabIndex = 0;
  open = false;
  isConnected = false;
  private parent: FakeHTMLElement | null = null;

  constructor(
    private readonly ownerDocument: FakeDocument,
    private readonly tagName: string
  ) {
  }

  appendChild(child: FakeHTMLElement): FakeHTMLElement {
    child.parent = this;
    child.isConnected = true;
    this.children.push(child);
    return child;
  }

  remove(): void {
    if (this.parent) {
      const index = this.parent.children.indexOf(this);
      if (index >= 0) {
        this.parent.children.splice(index, 1);
      }
    }

    this.parent = null;
    this.isConnected = false;
  }

  focus(_options?: FocusOptions): void {
    this.ownerDocument.activeElement = this;
  }

  select(): void {
  }

  setSelectionRange(_start: number, _end: number): void {
  }

  closest(selector: string): FakeHTMLElement | null {
    let candidate: FakeHTMLElement | null = this;
    while (candidate) {
      if (selector === 'dialog[open]' && candidate.tagName === 'dialog' && candidate.open) {
        return candidate;
      }

      candidate = candidate.parent;
    }

    return null;
  }

  querySelector(selector: string): FakeHTMLElement | null {
    for (const child of this.children) {
      if (child.tagName === selector) {
        return child;
      }

      const descendant = child.querySelector(selector);
      if (descendant) {
        return descendant;
      }
    }

    return null;
  }
}
