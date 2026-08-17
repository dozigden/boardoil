import { describe, expect, it } from 'vitest';
import accessTokenSecretModalSfc from '../components/AccessTokenSecretModal.vue?raw';
import oauthConnectionsViewSfc from '../views/OAuthConnectionsView.vue?raw';
import accessTokensViewSfc from '../../site/views/AccessTokensView.vue?raw';
import clientAccountsViewSfc from '../../system/views/ClientAccountsView.vue?raw';
import clientAccountTokensViewSfc from '../../system/views/ClientAccountTokensView.vue?raw';

describe('clipboard usage regressions', () => {
  it('keeps direct Clipboard API access out of application views and modals', () => {
    const clipboardConsumers = [
      accessTokenSecretModalSfc,
      oauthConnectionsViewSfc,
      accessTokensViewSfc,
      clientAccountsViewSfc,
      clientAccountTokensViewSfc
    ];

    for (const consumer of clipboardConsumers) {
      expect(consumer.includes('navigator.clipboard')).toBe(false);
    }
  });

  it('routes token, access-token setup, and OAuth setup copy actions through the shared helper', () => {
    expect(accessTokenSecretModalSfc.includes('copyTextToClipboard')).toBe(true);
    expect(accessTokensViewSfc.includes('copyTextToClipboard')).toBe(true);
    expect(oauthConnectionsViewSfc.includes('copyTextToClipboard')).toBe(true);
  });
});
