import { expect, test, getBaseUrl } from '../fixtures/boardOilTest';
import { CardEditorPage } from '../ui/CardEditorPage';

const platformCases: Array<{
  name: string;
  platform: string;
  modifier: 'Control' | 'Meta';
  label: 'Ctrl' | 'Cmd';
}> = [
  { name: 'non-Apple', platform: 'Linux x86_64', modifier: 'Control', label: 'Ctrl' },
  { name: 'Apple', platform: 'MacIntel', modifier: 'Meta', label: 'Cmd' }
];

for (const platformCase of platformCases) {
  test(`rich-text links use the ${platformCase.name} modifier`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard(`Markdown link tooltip ${platformCase.name}`);
    const targetUrl = `${getBaseUrl()}/about`;
    const card = await api.createCard(
      board,
      'Todo',
      `Markdown link tooltip ${platformCase.name} card`,
      `[BoardOil link](${targetUrl})`
    );
    const cardEditor = new CardEditorPage(page);
    const tooltip = `${platformCase.label}-click to open. Use the Link button to edit.`;

    await page.addInitScript(platform => {
      Object.defineProperty(navigator, 'platform', { configurable: true, value: platform });
    }, platformCase.platform);
    await page.goto(`/boards/${board.id}/card/${card.id}`);
    const link = cardEditor.descriptionEditor().getByRole('link', { name: 'BoardOil link' });

    await expect(link).toHaveAttribute('data-link-tooltip', tooltip);
    await expect(link).toHaveAttribute('aria-description', tooltip);
    await link.hover();
    await expect.poll(async () => {
      return link.evaluate(element => getComputedStyle(element, '::after').content);
    }).toContain(`${platformCase.label}-click to open`);

    const popupPromise = page.waitForEvent('popup');
    await link.click({ modifiers: [platformCase.modifier] });
    const popup = await popupPromise;
    await popup.waitForURL(targetUrl);
    await popup.close();
  });
}
