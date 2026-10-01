import { expect, test } from '../fixtures/boardOilTest';

for (const workspace of ['board', 'members'] as const) {
  test(`returning to the ${workspace} workspace ignores a failed snapshot from the board left behind`, async ({ api, authenticatedPage: page }) => {
    const firstBoard = await api.createBoard('Return to selected board');
    const secondBoard = await api.createBoard('Leave pending board');
    const path = `/boards/${firstBoard.id}${workspace === 'members' ? '/admin/members' : ''}`;
    await page.goto(path);
    await expect(page.getByRole('link', { name: 'Open current board', exact: true })).toHaveText(firstBoard.name);
    await expect(page.getByText('Loading board...', { exact: true })).toBeHidden();

    let release!: () => void;
    let requested!: () => void;
    const gate = new Promise<void>(resolve => { release = resolve; });
    const started = new Promise<void>(resolve => { requested = resolve; });
    await page.route(`**/api/boards/${secondBoard.id}`, async route => {
      requested();
      await gate;
      await route.fulfill({ status: 503, json: { success: false, message: 'Previous board unavailable.' } });
    }, { times: 1 });

    try {
      await page.getByRole('button', { name: 'Switch board', exact: true }).click();
      await page.getByRole('link', { name: `${secondBoard.name} #${secondBoard.id}`, exact: true }).click();
      await started;
      await expect(page.getByText('Loading board...', { exact: true })).toBeVisible();

      await page.goBack();
      await expect(page).toHaveURL(path);
      await expect(page.getByRole('link', { name: 'Open current board', exact: true })).toHaveText(firstBoard.name);
      await expect(page.getByText('Loading board...', { exact: true })).toBeHidden();
      const response = page.waitForResponse(result => result.url().endsWith(`/api/boards/${secondBoard.id}`));
      release();
      await (await response).finished();

      await expect(page).toHaveURL(path);
      await expect(page.getByRole('link', { name: 'Open current board', exact: true })).toHaveText(firstBoard.name);
      await expect(page.getByText('Previous board unavailable.', { exact: true })).toBeHidden();
      if (workspace === 'members') {
        await expect(page.getByRole('combobox')).toBeEnabled();
      }
    } finally {
      release();
    }
  });
}
