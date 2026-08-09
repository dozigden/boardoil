export async function copyTextToClipboard(text: string): Promise<boolean> {
  if (typeof navigator !== 'undefined' && navigator.clipboard?.writeText) {
    try {
      await navigator.clipboard.writeText(text);
      return true;
    } catch {
      // Fall back for browsers that expose the Clipboard API but deny access.
    }
  }

  if (
    typeof document === 'undefined'
    || !document.body
    || typeof document.execCommand !== 'function'
  ) {
    return false;
  }

  const textArea = document.createElement('textarea');
  const previouslyFocusedElement = document.activeElement instanceof HTMLElement
    ? document.activeElement
    : null;
  const copyContainer = previouslyFocusedElement?.closest('dialog[open]') ?? document.body;
  textArea.value = text;
  textArea.readOnly = true;
  textArea.tabIndex = -1;
  textArea.style.position = 'fixed';
  textArea.style.left = '-9999px';
  textArea.style.opacity = '0';
  copyContainer.appendChild(textArea);
  textArea.focus();
  textArea.select();
  textArea.setSelectionRange(0, text.length);

  try {
    return document.execCommand('copy');
  } catch {
    return false;
  } finally {
    textArea.remove();
    if (previouslyFocusedElement?.isConnected) {
      previouslyFocusedElement.focus({ preventScroll: true });
    }
  }
}
