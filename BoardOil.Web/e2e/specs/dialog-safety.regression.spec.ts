import type { Locator, Page } from '@playwright/test';
import { expect, test } from '../fixtures/boardOilTest';
import { BoardPage } from '../ui/BoardPage';

type ViewportScenario = {
  height: number;
  name: string;
  scrollRegionSelector: string;
  width: number;
};

const cardEditorScenarios: ViewportScenario[] = [
  {
    name: 'short desktop',
    width: 1100,
    height: 500,
    scrollRegionSelector: '.card-editor-main'
  },
  {
    name: 'mobile',
    width: 390,
    height: 650,
    scrollRegionSelector: '.card-editor-layout'
  }
];

test('short shared modal preserves native close, focus, backdrop, and submit behaviour', async ({ authenticatedPage: page }) => {
  await page.goto('/');

  const createBoardButton = page.getByRole('button', { name: 'Create board', exact: true });
  const createBoardHeading = page.getByRole('heading', { name: 'Create Board' });
  const dialog = page.getByRole('dialog').filter({ has: createBoardHeading });

  await test.step('Escape closes the dialog and restores trigger focus', async () => {
    await createBoardButton.focus();
    await createBoardButton.click();
    await expect(dialog).toBeVisible();
    await expect.poll(async () => await dialog.evaluate(element => element.contains(document.activeElement))).toBe(true);

    await page.keyboard.press('Escape');

    await expect(dialog).toBeHidden();
    await expect(createBoardButton).toBeFocused();
  });

  await test.step('a backdrop click closes the dialog and restores trigger focus', async () => {
    await createBoardButton.click();
    await expect(dialog).toBeVisible();

    await page.mouse.click(1, 1);

    await expect(dialog).toBeHidden();
    await expect(createBoardButton).toBeFocused();
  });

  await test.step('the footer submit button remains part of the dialog form', async () => {
    await createBoardButton.click();
    await dialog.getByLabel('Board name').fill('Dialog submit regression');
    await dialog.getByRole('button', { name: 'Create board', exact: true }).click();

    await expect(dialog).toBeHidden();
    await expect(page).toHaveURL(/\/boards\/\d+$/);
  });
});

test('card-type emoji picker remains reachable at constrained height', async ({ api, authenticatedPage: page }) => {
  await page.setViewportSize({ width: 900, height: 520 });
  const board = await api.createBoard('Dialog overlay regression');
  await page.goto(`/boards/${board.id}/admin/card-types`);
  await page.getByRole('button', { name: /^Edit card type / }).first().click();

  const cardTypeHeading = page.getByRole('heading', { name: /^Edit Card Type/ });
  const cardTypeDialog = page.getByRole('dialog').filter({ has: cardTypeHeading });
  await expect(cardTypeDialog).toBeVisible();
  const emojiTrigger = cardTypeDialog.getByRole('button', { name: 'Emoji', exact: true });
  await emojiTrigger.click();

  const emojiPanel = cardTypeDialog.getByRole('dialog', { name: 'Emoji picker' });
  await expect(emojiPanel).toBeVisible();
  await expectPanelToBeReachable(emojiPanel, page);
  await emojiPanel.getByRole('menuitem').first().click();
  await expect(emojiPanel).toBeHidden();
  await expect(emojiTrigger).not.toContainText('Select emoji');
});

test('card-editor column dropdown remains reachable at constrained height', async ({ api, authenticatedPage: page }) => {
  await page.setViewportSize({ width: 900, height: 520 });
  const board = await api.createBoard('Dialog card dropdown regression');
  const cardTitle = 'Overlay test card';
  await api.createCard(board, 'Todo', cardTitle);

  const boardPage = new BoardPage(page);
  await boardPage.open(board.id);
  await boardPage.openCard('Todo', cardTitle);

  const dialog = page.getByRole('dialog');
  await dialog.getByTitle('Select column').click();
  const columnMenu = dialog.getByRole('menu', { name: 'Select column' });
  await expectPanelToBeReachable(columnMenu, page);
  await columnMenu.getByRole('button', { name: 'In Progress', exact: true }).click();
  await expect(columnMenu).toBeHidden();
});

for (const scenario of cardEditorScenarios) {
  test(`card editor keeps its chrome fixed while its managed body scrolls at ${scenario.name} size`, async ({ api, authenticatedPage: page }) => {
    await page.setViewportSize({ width: scenario.width, height: scenario.height });
    const board = await api.createBoard(`Dialog ${scenario.name} regression`);
    const cardTitle = `${scenario.name} dialog card`;
    const description = createLongDescription();
    await api.createCard(board, 'Todo', cardTitle, description);

    const boardPage = new BoardPage(page);
    await boardPage.open(board.id);
    await boardPage.openCard('Todo', cardTitle);

    const dialog = page.getByRole('dialog');
    const title = dialog.locator('.fixed-chrome-dialog__title');
    const saveButton = dialog.getByRole('button', { name: 'Save card' });
    const scrollRegion = dialog.locator(scenario.scrollRegionSelector);

    await expect(title).toBeVisible();
    await expect(saveButton).toBeVisible();
    await expect.poll(async () => await hasVerticalOverflow(scrollRegion)).toBe(true);

    const titleBefore = await requireBoundingBox(title);
    const saveBefore = await requireBoundingBox(saveButton);
    await scrollRegion.evaluate(element => {
      element.scrollTop = element.scrollHeight;
    });
    await expect.poll(async () => await scrollRegion.evaluate(element => element.scrollTop)).toBeGreaterThan(0);

    const titleAfter = await requireBoundingBox(title);
    const saveAfter = await requireBoundingBox(saveButton);
    expect(titleAfter.y).toBeCloseTo(titleBefore.y, 0);
    expect(saveAfter.y).toBeCloseTo(saveBefore.y, 0);
    await expectFullyInsideViewport(title, page);
    await expectFullyInsideViewport(saveButton, page);
  });
}

test('standard constrained-height dialog keeps header and footer visible while its body scrolls', async ({ authenticatedPage: page }) => {
  await page.setViewportSize({ width: 900, height: 280 });
  await page.goto('/');
  await page.getByRole('button', { name: 'Create board', exact: true }).click();

  const heading = page.getByRole('heading', { name: 'Create Board' });
  const dialog = page.getByRole('dialog').filter({ has: heading });
  const submitButton = dialog.getByRole('button', { name: 'Create board', exact: true });
  const descriptionInput = dialog.getByLabel('Description (optional)');

  await expect(dialog).toBeVisible();
  await expect(heading).toBeVisible();
  await expect(submitButton).toBeVisible();
  await expectFullyInsideViewport(heading, page);
  await expectFullyInsideViewport(submitButton, page);
  const headingBefore = await requireBoundingBox(heading);
  const submitBefore = await requireBoundingBox(submitButton);
  const descriptionBefore = await requireBoundingBox(descriptionInput);

  await descriptionInput.hover();
  await page.mouse.wheel(0, 1000);
  await expect.poll(async () => (await requireBoundingBox(descriptionInput)).y).not.toBeCloseTo(descriptionBefore.y, 0);

  const headingAfter = await requireBoundingBox(heading);
  const submitAfter = await requireBoundingBox(submitButton);
  expect(headingAfter.y).toBeCloseTo(headingBefore.y, 0);
  expect(submitAfter.y).toBeCloseTo(submitBefore.y, 0);
  await expectFullyInsideViewport(heading, page);
  await expectFullyInsideViewport(submitButton, page);
});

async function expectPanelToBeReachable(panel: Locator, page: Page) {
  await expect(panel).toBeVisible();
  const geometry = await panel.evaluate(element => {
    const bounds = element.getBoundingClientRect();
    const centreX = bounds.left + bounds.width / 2;
    const centreY = bounds.top + bounds.height / 2;
    const elementAtCentre = document.elementFromPoint(centreX, centreY);
    return {
      bottom: bounds.bottom,
      left: bounds.left,
      right: bounds.right,
      top: bounds.top,
      centreIsReachable: elementAtCentre !== null && element.contains(elementAtCentre)
    };
  });
  const viewport = page.viewportSize();
  expect(viewport).not.toBeNull();
  expect(geometry.left).toBeGreaterThanOrEqual(0);
  expect(geometry.top).toBeGreaterThanOrEqual(0);
  expect(geometry.right).toBeLessThanOrEqual(viewport!.width);
  expect(geometry.bottom).toBeLessThanOrEqual(viewport!.height);
  expect(geometry.centreIsReachable).toBe(true);
}

async function expectFullyInsideViewport(locator: Locator, page: Page) {
  const bounds = await requireBoundingBox(locator);
  const viewport = page.viewportSize();
  expect(viewport).not.toBeNull();
  expect(bounds.x).toBeGreaterThanOrEqual(0);
  expect(bounds.y).toBeGreaterThanOrEqual(0);
  expect(bounds.x + bounds.width).toBeLessThanOrEqual(viewport!.width);
  expect(bounds.y + bounds.height).toBeLessThanOrEqual(viewport!.height);
}

async function hasVerticalOverflow(locator: Locator) {
  return await locator.evaluate(element => element.scrollHeight > element.clientHeight);
}

async function requireBoundingBox(locator: Locator) {
  const bounds = await locator.boundingBox();
  expect(bounds).not.toBeNull();
  return bounds!;
}

function createLongDescription() {
  return Array.from(
    { length: 80 },
    (_, index) => `Paragraph ${index + 1}: dialog layout regression content that requires vertical scrolling.`
  ).join('\n\n');
}
