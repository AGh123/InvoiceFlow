import { test, expect } from '@playwright/test';

async function openInteractiveEditor(page) {
  await page.goto('/invoices/new');
  const date = page.locator('#issue-date');
  await expect(async () => {
    await date.click();
    await expect(date).toHaveAttribute('aria-expanded', 'true', { timeout: 1000 });
  }).toPass({ timeout: 10_000 });
  await page.locator('.date-picker__grid button:focus').press('Escape');
  await expect(date).toHaveAttribute('aria-expanded', 'false');
}

test('unsaved navigation dialog traps focus and restores it', async ({ page }) => {
  await openInteractiveEditor(page);
  await page.locator('#customer-name').fill('Browser draft');
  await page.getByRole('button', { name: 'Cancel' }).click();

  const dialog = page.getByRole('dialog', { name: 'Discard unsaved changes?' });
  await expect(dialog).toBeVisible();
  await expect(dialog.getByRole('button', { name: 'Keep editing' })).toBeFocused();
  await page.keyboard.press('Tab');
  await expect(dialog.getByRole('button', { name: 'Discard changes' })).toBeFocused();
  await page.keyboard.press('Tab');
  await expect(dialog.getByRole('button', { name: 'Keep editing' })).toBeFocused();

  await page.keyboard.press('Escape');
  await expect(dialog).toBeHidden();
  await expect(page.locator('#customer-name')).toHaveValue('Browser draft');
  await page.getByRole('button', { name: 'Cancel' }).click();
  await dialog.getByRole('button', { name: 'Discard changes' }).click();
  await expect(page).toHaveURL(/\/invoices$/);
});

test('date and currency controls respond to keyboard and outside interaction', async ({ page }) => {
  await openInteractiveEditor(page);

  const date = page.locator('#issue-date');
  await date.click();
  await expect(page.getByRole('dialog', { name: 'Choose date' })).toBeVisible();
  const focusedDay = page.locator('.date-picker__grid button:focus');
  await expect(focusedDay).toHaveCount(1);
  const firstFocusedLabel = await focusedDay.getAttribute('aria-label');
  await page.keyboard.press('ArrowRight');
  await expect(focusedDay).not.toHaveAttribute('aria-label', firstFocusedLabel);
  await page.keyboard.press('Escape');
  await expect(page.getByRole('dialog', { name: 'Choose date' })).toBeHidden();
  await expect(date).toBeFocused();

  const currency = page.locator('#currency-code');
  await currency.fill('eu');
  await expect(page.getByRole('listbox', { name: 'Common currencies' })).toBeVisible();
  await currency.press('ArrowDown');
  await currency.press('Enter');
  await expect(currency).toHaveValue('EUR');
  await currency.click();
  await expect(page.getByRole('listbox', { name: 'Common currencies' })).toBeVisible();
  await page.getByRole('heading', { name: 'New Invoice' }).click();
  await expect(page.getByRole('listbox', { name: 'Common currencies' })).toBeHidden();
});

test('create, edit, and delete an invoice on desktop and mobile', async ({ page }) => {
  const number = `BROWSER-${Date.now()}`;
  await openInteractiveEditor(page);
  await page.locator('#invoice-number').fill(number);
  await page.locator('#customer-name').fill('Browser customer');
  await page.getByRole('button', { name: 'Add Line Item' }).first().click();
  await page.locator('.line-items-editor__field--description input').fill('Consulting');
  await page.locator('.line-items-editor__field--quantity input').fill('2');
  await page.locator('.line-items-editor__field--price input').fill('25');
  await page.getByRole('button', { name: 'Save Invoice' }).click();
  await expect(page).toHaveURL(/\/invoices$/);

  await page.locator('#invoice-search').fill(number);
  const row = page.locator('.invoice-list__desktop tr').filter({ hasText: number });
  await expect(row).toContainText('USD 50.00');
  await row.getByRole('link', { name: number }).click();
  await page.locator('#customer-name').fill('Updated browser customer');
  await page.getByRole('button', { name: 'Save Invoice' }).click();
  await expect(page).toHaveURL(/\/invoices$/);

  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.locator('.invoice-list__mobile')).toBeVisible();
  await page.locator('#invoice-search').fill(number);
  const card = page.locator('.invoice-list__card').filter({ hasText: number });
  await expect(card).toContainText('Updated browser customer');
  await card.getByRole('button', { name: `Actions for invoice ${number}` }).click();
  await expect(card.getByRole('menu', { name: `Actions for invoice ${number}` })).toBeVisible();
  await card.getByRole('menuitem', { name: 'Delete' }).click();
  const dialog = page.getByRole('dialog', { name: 'Delete invoice?' });
  await expect(dialog.getByRole('button', { name: 'Cancel' })).toBeFocused();
  await dialog.getByRole('button', { name: 'Delete Invoice' }).click();
  await expect(card).toHaveCount(0);
});
