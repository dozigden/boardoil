import { expect, test } from '../fixtures/boardOilTest';
import { holdBoardSaveResponse, offerApplicationUpdate } from '../support/appUpdate';

test('an available update waits for saves and reloads immediately when clicked', async ({ api, authenticatedPage: page }, testInfo) => {
  const board = await api.createBoard('Application update');
  await offerApplicationUpdate(page);
  await page.goto(`/boards/${board.id}/admin/details`);
  const banner = page.getByRole('complementary', { name: 'Application update', exact: true });
  const refresh = banner.getByRole('button', { name: 'Reload', exact: true });
  const name = page.getByRole('textbox', { name: 'Board name', exact: true });
  const draftName = `${board.name} edited`;

  await expect(banner).toBeVisible();
  await name.fill(draftName);

  await test.step('block refresh until the save response arrives', async () => {
    const heldSave = await holdBoardSaveResponse(page, board.id);
    try {
      await page.getByRole('button', { name: 'Save details', exact: true }).click();
      await heldSave.ready;
      await refresh.click();
      await expect(banner).toContainText('A save or upload is still in progress');
    } finally {
      heldSave.release();
    }
    await expect(page.getByRole('link', { name: 'Open current board', exact: true })).toHaveText(draftName);
  });

  await test.step('reload the current URL immediately when clicked', async () => {
    const navigation = page.waitForRequest(request => request.isNavigationRequest());
    await refresh.click();
    await navigation;
    await expect(page).toHaveURL(`/boards/${board.id}/admin/details`);
    await expect(name).toHaveValue(draftName);
    await expect(banner).toBeVisible();
  });

  await page.screenshot({ path: testInfo.outputPath('update-banner-desktop.png') });
  await page.setViewportSize({ width: 390, height: 844 });
  await expect(refresh).toBeInViewport();
  await page.screenshot({ path: testInfo.outputPath('update-banner-mobile.png') });
});
