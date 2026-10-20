import { test, expect } from '@playwright/test';
import type { Page } from '@playwright/test';
import { readFile } from 'node:fs/promises';
import catalog from '../src/default-triages.json' with { type: 'json' };

async function individual(page: Page) {
  await page.getByRole('button', { name: /Triagem Individual/ }).click();
  await page.getByRole('button', { name: 'Continuar', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Triagens disponíveis' })).toBeVisible();
  await expect(page.locator('.triage-card')).toHaveCount(catalog.length);
}

test('A primeira tela escolhe o modo; grupo pede login e não abre o SQLite', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'Como você quer fazer a triagem?' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Continuar', exact: true })).toBeDisabled();
  await page.screenshot({ path: '../artifacts/web-verification/escolha-desktop.png' });
  await page.getByRole('button', { name: /Triagem em Grupo/ }).click();
  await page.getByRole('button', { name: 'Continuar', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Bem-vindo(a) de volta!' })).toBeVisible();
  await expect(page).toHaveURL(/\/login$/);
  expect(await page.evaluate(async () => (await indexedDB.databases()).map(db => db.name))).not.toContain('triar-individual-sqlite');
  await page.getByRole('button', { name: '← Voltar' }).click();
  await expect(page.getByRole('heading', { name: 'Como você quer fazer a triagem?' })).toBeVisible();
});

test('Individual calcula opções, persiste SQLite após recarga e nunca envia dados à API', async ({ page, context }) => {
  const apiCalls: string[] = [];
  page.on('request', req => { if (new URL(req.url()).pathname.startsWith('/api/')) apiCalls.push(req.url()); });
  await page.goto('/');
  await individual(page);
  await page.screenshot({ path: '../artifacts/web-verification/home-atual-desktop.png', fullPage: true });
  await page.locator('.triage-card').first().getByRole('link', { name: 'Entrar', exact: true }).click();
  await page.getByLabel('Nome completo', { exact: true }).fill('Paciente somente local');
  await page.locator('select[name=escolaridade]').selectOption('Ensino médio completo');
  await page.getByLabel('Doenças prévias (opcional)', { exact: true }).fill('Informação local de teste');
  await page.getByLabel('Idade', { exact: true }).fill('40');
  await page.locator('select[name=sexo]').selectOption('Outro');
  for (const radio of await page.getByRole('radio', { name: 'Sim', exact: true }).all()) await radio.check();
  for (const checkbox of await page.locator('.option-list input[type=checkbox]').all()) await checkbox.check();
  await page.getByRole('button', { name: 'Finalizar triagem ✓' }).click();
  const max = catalog[0].perguntas.reduce((total, p) => total + p.peso * Math.max(1, p.opcoes.length), 0);
  await expect(page.locator('.score-circle strong')).toHaveText(String(max));
  await expect(page.getByText('Informação local de teste', { exact: true })).toBeVisible();
  await page.getByRole('link', { name: 'Ver histórico desta triagem' }).click();
  await expect(page.getByRole('heading', { name: 'Paciente somente local' })).toBeVisible();
  const download = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Exportar Excel' }).click();
  const excel = await download;
  expect((await readFile((await excel.path())!)).subarray(0, 2).toString()).toBe('PK');
  await page.reload();
  await individual(page);
  await page.getByRole('link', { name: 'Histórico geral', exact: true }).first().click();
  await expect(page.getByRole('heading', { name: 'Paciente somente local' })).toBeVisible();
  await page.getByRole('link', { name: '← Voltar para a home' }).click();
  await page.getByRole('link', { name: 'Sobre o projeto', exact: true }).first().click();
  const backup = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Baixar cópia do SQLite' }).click();
  const file = await backup;
  expect((await readFile((await file.path())!)).subarray(0, 15).toString()).toBe('SQLite format 3');
  await page.getByRole('link', { name: '← Voltar para a home' }).click();
  await page.getByRole('button', { name: 'Trocar modo de triagem', exact: true }).first().click();
  await page.getByRole('button', { name: /Triagem em Grupo/ }).click();
  await page.getByRole('button', { name: 'Continuar', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Bem-vindo(a) de volta!' })).toBeVisible();
  expect(apiCalls).toEqual([]);
  await context.setOffline(false);
});

test('Home mobile segue o MAUI sem overflow e os dados locais sobrevivem offline', async ({ page, context }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');
  await page.screenshot({ path: '../artifacts/web-verification/escolha-mobile.png', fullPage: true });
  await individual(page);
  await expect(page.locator('.bottom-nav')).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: '../artifacts/web-verification/home-atual-mobile.png', fullPage: true });
  await page.evaluate(async () => { await navigator.serviceWorker.ready; });
  await expect.poll(() => page.evaluate(() => Boolean(navigator.serviceWorker.controller))).toBe(true);
  await context.setOffline(true);
  await page.reload();
  await individual(page);
  await expect(page.locator('.triage-card')).toHaveCount(catalog.length);
  await context.setOffline(false);
});

test('Erro no armazenamento impede entrada individual e não simula sucesso', async ({ page }) => {
  await page.addInitScript(() => { indexedDB.open = () => { throw new Error('Armazenamento bloqueado para teste'); }; });
  await page.goto('/');
  await page.getByRole('button', { name: /Triagem Individual/ }).click();
  await page.getByRole('button', { name: 'Continuar', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('Armazenamento bloqueado');
  await expect(page.getByRole('heading', { name: 'Como você quer fazer a triagem?' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Continuar', exact: true })).toBeEnabled();
});
