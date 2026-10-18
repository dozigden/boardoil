import { expect, test } from '../fixtures/boardOilTest';
import { holdCardCreationResponse } from '../support/cardCreationResponse';
import { controlLayoutAnimations, seekLayoutAnimations } from '../support/layoutAnimations';
import { BoardPage } from '../ui/BoardPage';
import { CardEditorPage } from '../ui/CardEditorPage';

for (const renderer of ['full', 'lite']) {
  for (const reducedMotion of ['no-preference', 'reduce'] as const) {
    test(`Enter continues from creation to description with ${renderer} goo and ${reducedMotion} motion`, async ({ api, authenticatedPage: page }) => {
      const board = await api.createBoard(`Creation keyboard ${renderer} ${reducedMotion}`);
      await page.emulateMedia({ reducedMotion });
      await controlLayoutAnimations(page);
      await page.goto(`/boards/${board.id}?gooRenderer=${renderer}`);
      const bp = new BoardPage(page);
      const column = bp.column('Todo');
      await column.getByRole('button', { name: 'Add default card' }).click();
      await seekLayoutAnimations(page, 1);
      const title = column.getByPlaceholder('Title');
      await title.fill('Keyboard created');
      await page.keyboard.press('Enter');

      const card = bp.card('Todo', 'Keyboard created');
      await expect(title).toHaveCount(0);
      await expect(card).toBeFocused();
      // Creation animation is still paused: focus must not wait for it to finish.
      await page.keyboard.press('Enter');
      const editor = new CardEditorPage(page);
      await expect(editor.descriptionEditor()).toBeFocused();
      await page.keyboard.press('Escape');
      await expect(page.getByRole('dialog')).toBeHidden();
      await expect(card).toBeFocused();

      await page.keyboard.press('Enter');
      await expect(editor.descriptionEditor()).toBeFocused();
      await page.keyboard.insertText('Start writing immediately');
      await expect(editor.descriptionEditor()).toHaveText('Start writing immediately');
      await editor.saveCard();
      await expect(card).toBeFocused();
    });
  }
}

test('a delayed create preserves focus when the user moves elsewhere', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Creation focus moved elsewhere');
  const bp = new BoardPage(page);
  await bp.open(board.id);
  const column = bp.column('Todo');
  await column.getByRole('button', { name: 'Add default card' }).click();
  const title = column.getByPlaceholder('Title');
  await title.fill('Saved in the background');
  const response = await holdCardCreationResponse(page, board.id);
  try {
    await page.keyboard.press('Enter');
    await response.ready;
    const search = page.getByPlaceholder('Search', { exact: true });
    await search.focus();
    response.release();

    await expect(title).toHaveCount(0);
    await expect(bp.card('Todo', 'Saved in the background')).toBeVisible();
    await expect(search).toBeFocused();
  } finally {
    response.release();
  }
});

test('remote creation leaves an unfinished local title focused', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Remote creation focus');
  const bp = new BoardPage(page);
  await bp.open(board.id);
  const column = bp.column('Todo');
  await column.getByRole('button', { name: 'Add default card' }).click();
  const title = column.getByPlaceholder('Title');
  await title.fill('Still writing');

  await api.createCard(board, 'Todo', 'Remote arrival');

  await expect(bp.card('Todo', 'Remote arrival')).toBeVisible();
  await expect(title).toBeFocused();
  await expect(title).toHaveValue('Still writing');
});

test('a filtered-out creation does not focus another card', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Filtered creation focus');
  await api.createCard(board, 'Todo', 'Visible existing card');
  const bp = new BoardPage(page);
  await bp.open(board.id);
  await page.getByPlaceholder('Search', { exact: true }).fill('Visible');
  const column = bp.column('Todo');
  await column.getByRole('button', { name: 'Add default card' }).click();
  const title = column.getByPlaceholder('Title');
  await title.fill('Hidden new card');
  await page.keyboard.press('Enter');

  await expect(title).toHaveCount(0);
  await expect(bp.card('Todo', 'Hidden new card')).toHaveCount(0);
  await expect(bp.card('Todo', 'Visible existing card')).not.toBeFocused();
  await expect(page.getByRole('dialog')).toBeHidden();
});

for (const opening of ['mouse', 'direct route']) {
  test(`opening an older blank card by ${opening} focuses its description once`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard(`Empty description focus ${opening}`);
    const card = await api.createCard(board, 'Todo', 'Existing empty card');
    if (opening === 'mouse') {
      const bp = new BoardPage(page);
      await bp.open(board.id);
      await bp.card('Todo', card.title).click();
    } else {
      await page.goto(`/boards/${board.id}/card/${card.id}`);
    }
    const editor = new CardEditorPage(page);
    await expect(editor.descriptionEditor()).toBeFocused();
    await editor.commentEditor().fill('Keep this unfinished comment');

    const catalogueRefresh = page.waitForResponse(response => response.url().endsWith(`/api/boards/${board.id}/tags`)
      && response.request().method() === 'GET');
    await api.updateCard(board.id, card, { title: 'Updated remotely', tagNames: ['New remote tag'] });

    expect((await catalogueRefresh).ok()).toBe(true);
    await expect(editor.commentEditor()).toHaveText('Keep this unfinished comment');
    await expect(editor.commentEditor()).toBeFocused();
  });
}

test('a populated description retains the existing initial dialog focus', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Populated description focus');
  const card = await api.createCard(board, 'Todo', 'Existing populated card', 'Already written');
  await page.goto(`/boards/${board.id}/card/${card.id}`);

  await expect(new CardEditorPage(page).descriptionEditor()).toHaveText('Already written');
  await expect(page.getByRole('button', { name: 'Card actions', exact: true })).toBeFocused();
});
