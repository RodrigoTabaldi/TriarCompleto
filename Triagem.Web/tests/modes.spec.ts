import { test, expect } from '@playwright/test';
import type { Page } from '@playwright/test';
import { readFile } from 'node:fs/promises';
import catalog from '../src/default-triages.json' with { type: 'json' };
import initSqlJs from 'sql.js';

async function individual(page: Page) {
  await page.getByRole('button', { name: /Triagem Individual/ }).click();
  await page.getByRole('button', { name: 'Continuar', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Triagens disponíveis' })).toBeVisible();
  await expect(page.locator('.triage-card')).toHaveCount(2);
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
  for (const field of await page.locator('.option-list').all()) await field.locator('input[type=radio]').first().check();
  await page.getByRole('button', { name: 'Finalizar triagem ✓' }).click();
  await expect(page.locator('.score-circle strong')).toHaveText('53');
  await expect(page.getByText('Informação local de teste', { exact: true })).toBeVisible();
  const protectedData = await page.evaluate(async () => {
    const db = await new Promise<IDBDatabase>((resolve, reject) => { const open = indexedDB.open('triar-individual-sqlite'); open.onsuccess = () => resolve(open.result); open.onerror = () => reject(open.error); });
    const read = (key: string) => new Promise<unknown>((resolve, reject) => { const request = db.transaction('files').objectStore('files').get(key); request.onsuccess = () => resolve(request.result); request.onerror = () => reject(request.error); });
    const bytes = await read('triar.db') as Uint8Array;
    const key = await read('triar-key') as CryptoKey;
    db.close();
    return { header: new TextDecoder().decode(bytes.slice(0, 8)), content: new TextDecoder().decode(bytes), extractable: key.extractable };
  });
  expect(protectedData.header).toBe('TRIARDB1');
  expect(protectedData.content).not.toContain('Paciente somente local');
  expect(protectedData.extractable).toBe(false);
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
  await expect(page.locator('.triage-card')).toHaveCount(2);
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

test('Chave local ausente impede abertura sem substituir o banco', async ({ page }) => {
  await page.goto('/');
  await individual(page);
  await page.evaluate(async () => {
    const db = await new Promise<IDBDatabase>((resolve, reject) => { const open = indexedDB.open('triar-individual-sqlite'); open.onsuccess = () => resolve(open.result); open.onerror = () => reject(open.error); });
    await new Promise<void>((resolve, reject) => { const tx = db.transaction('files', 'readwrite'); tx.objectStore('files').delete('triar-key'); tx.oncomplete = () => resolve(); tx.onerror = () => reject(tx.error); });
    db.close();
  });
  await page.reload();
  await page.getByRole('button', { name: /Triagem Individual/ }).click();
  await page.getByRole('button', { name: 'Continuar', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('chave do banco local não foi encontrada');
  const header = await page.evaluate(async () => {
    const db = await new Promise<IDBDatabase>((resolve, reject) => { const open = indexedDB.open('triar-individual-sqlite'); open.onsuccess = () => resolve(open.result); open.onerror = () => reject(open.error); });
    const bytes = await new Promise<Uint8Array>((resolve, reject) => { const request = db.transaction('files').objectStore('files').get('triar.db'); request.onsuccess = () => resolve(request.result); request.onerror = () => reject(request.error); });
    db.close(); return new TextDecoder().decode(bytes.slice(0, 8));
  });
  expect(header).toBe('TRIARDB1');
});

test('Fonoaudiologia exige consistência e inverte as respostas de proteção', async ({ page }) => {
  await page.goto('/');
  await individual(page);
  await page.locator('.triage-card').filter({ hasText: 'Triagem Fonoaudiológica para pessoa idosa' }).getByRole('link', { name: 'Entrar', exact: true }).click();
  await page.getByLabel('Nome completo', { exact: true }).fill('Teste fono');
  await page.getByLabel('Idade', { exact: true }).fill('70');
  for (const radio of await page.getByRole('radio', { name: 'Não', exact: true }).all()) await radio.check();
  await page.getByRole('button', { name: 'Finalizar triagem ✓' }).click();
  const fono = catalog.find(item => item.titulo === 'Triagem Fonoaudiológica para pessoa idosa')!;
  const score = fono.perguntas.filter(p => [10, 11, 12, 13, 15, 20].includes(p.ordem)).reduce((sum, p) => sum + p.peso, 0);
  await expect(page.locator('.score-circle strong')).toHaveText(String(score));
});

test('Atualização do catálogo preserva triagens e histórico existentes', async ({ page }) => {
  const SQL = await initSqlJs();
  const db = new SQL.Database();
  db.run(`CREATE TABLE Usuarios(Id INTEGER PRIMARY KEY,Nome TEXT,Email TEXT);
    CREATE TABLE TriagemModelos(Id INTEGER PRIMARY KEY AUTOINCREMENT,Titulo TEXT,PublicoAlvo TEXT,Descricao TEXT,Icone TEXT,CriadorUsuarioId INTEGER,Ativa INTEGER DEFAULT 1);
    CREATE TABLE Perguntas(Id INTEGER PRIMARY KEY AUTOINCREMENT,TriagemModeloId INTEGER,Texto TEXT,Peso INTEGER,Ordem INTEGER,Categoria TEXT,OpcoesJson TEXT);
    CREATE TABLE FaixasResultado(Id INTEGER PRIMARY KEY AUTOINCREMENT,TriagemModeloId INTEGER,Titulo TEXT,Recomendacao TEXT,PontuacaoMin INTEGER,PontuacaoMax INTEGER,Cor TEXT,Ordem INTEGER);
    CREATE TABLE UsuarioTriagensHome(UsuarioId INTEGER,TriagemModeloId INTEGER,Visivel INTEGER,Ordem INTEGER,PRIMARY KEY(UsuarioId,TriagemModeloId));
    CREATE TABLE TriagemResultados(Id INTEGER PRIMARY KEY AUTOINCREMENT,TriagemModeloId INTEGER,UsuarioId INTEGER,DadosJson TEXT);
    CREATE TABLE RespostasDadas(Id INTEGER PRIMARY KEY AUTOINCREMENT,TriagemResultadoId INTEGER,PerguntaId INTEGER,Valor INTEGER,OpcoesSelecionadasJson TEXT);
    INSERT INTO Usuarios VALUES(1,'Individual','');
    INSERT INTO TriagemModelos VALUES(1,'Protocolo anterior','','','',NULL,1);
    INSERT INTO TriagemModelos VALUES(2,'Minha triagem','','','',1,1);
    PRAGMA user_version=1;`);
  db.run('INSERT INTO TriagemResultados(TriagemModeloId,UsuarioId,DadosJson) VALUES(1,1,?)', [JSON.stringify({ triagemModeloId: 1, tituloTriagem: 'Protocolo anterior', nomePaciente: 'Histórico preservado', idade: 40, sexo: '', pontuacao: 1, pontuacaoMaxima: 2, classificacao: 'Anterior', cor: '#10B981', data: new Date().toISOString() })]);
  const bytes = Array.from(db.export());
  db.close();
  await page.goto('/');
  await page.evaluate(async data => {
    await new Promise<void>((resolve, reject) => {
      const request = indexedDB.open('triar-individual-sqlite', 1);
      request.onupgradeneeded = () => request.result.createObjectStore('files');
      request.onerror = () => reject(request.error);
      request.onsuccess = () => {
        const tx = request.result.transaction('files', 'readwrite');
        tx.objectStore('files').put(new Uint8Array(data), 'triar.db');
        tx.oncomplete = () => { request.result.close(); resolve(); };
        tx.onerror = () => reject(tx.error);
      };
    });
  }, bytes);
  await individual(page);
  await page.getByRole('link', { name: 'Histórico geral', exact: true }).first().click();
  await expect(page.getByRole('heading', { name: 'Histórico preservado' })).toBeVisible();
  await page.getByRole('link', { name: '← Voltar para a home' }).click();
  await page.getByRole('button', { name: 'Triagens específicas' }).click();
  await expect(page.getByRole('heading', { name: 'Minha triagem' })).toBeVisible();
});

test('Triagem personalizada conserva imagem local', async ({ page }) => {
  await page.goto('/');
  await individual(page);
  await page.getByRole('link', { name: 'Criar sua triagem', exact: true }).first().click();
  await page.getByLabel('Título', { exact: true }).fill('Triagem com imagem');
  await page.getByLabel('Pergunta 1').fill('Existe algum sintoma?');
  await page.getByLabel('Imagem ilustrativa').setInputFiles({ name: 'imagem.png', mimeType: 'image/png', buffer: Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/aZkAAAAASUVORK5CYII=', 'base64') });
  await expect(page.getByAltText('Prévia da imagem')).toBeVisible();
  await page.getByRole('button', { name: 'Salvar triagem ✓' }).click();
  await page.getByRole('button', { name: 'Triagens específicas' }).click();
  await expect(page.locator('.triage-card').filter({ hasText: 'Triagem com imagem' }).locator('img')).toHaveAttribute('src', /^data:image\/png;base64,/);
});

test('Todas as triagens padrão aplicam pesos e faixas do catálogo MAUI', async ({ page }) => {
  const apiCalls: string[] = [];
  page.on('request', request => { if (new URL(request.url()).pathname.startsWith('/api/')) apiCalls.push(request.url()); });
  await page.goto('/');
  await individual(page);
  for (const model of catalog) {
    for (const yes of [false, true]) {
      if (model.id > 2) await page.getByRole('button', { name: 'Triagens específicas' }).click();
      else await page.getByRole('button', { name: 'Triagens principais' }).click();
      await page.locator('.triage-card').filter({ has: page.getByRole('heading', { name: model.titulo, exact: true }) }).getByRole('link', { name: 'Entrar', exact: true }).click();
      await page.getByLabel('Nome completo', { exact: true }).fill('Paciente de teste');
      await page.getByLabel('Idade', { exact: true }).fill('42');
      for (const question of await page.locator('fieldset.question').all()) {
        const single = question.locator('.option-list input[type=radio]');
        if (await single.count()) await single.first().check();
        else {
          await question.getByRole('radio', { name: yes ? 'Sim' : 'Não', exact: true }).check();
          if (yes) for (const option of await question.locator('.option-list input[type=checkbox]').all()) await option.check();
        }
      }
      let score = 0;
      for (const question of model.perguntas) {
        if (model.titulo === 'Classificação do risco vocal') {
          if (question.ordem === 1) continue;
          if (question.ordem === 22) { score += 1; continue; }
          if ((question.ordem >= 15 && question.ordem <= 17) ? !yes : yes) score += question.peso;
        } else if (model.titulo === 'Triagem Fonoaudiológica para pessoa idosa') {
          if ([10, 11, 12, 13, 15, 20].includes(question.ordem) ? !yes : yes) score += question.peso;
        } else if (yes) score += question.peso;
      }
      const range = model.faixas.find(f => score >= f.pontuacaoMin && score <= f.pontuacaoMax)!;
      await page.getByRole('button', { name: 'Finalizar triagem ✓' }).click();
      await expect(page.locator('.score-circle strong')).toHaveText(String(score));
      await expect(page.getByRole('heading', { name: range.titulo, exact: true })).toBeVisible();
      await page.getByRole('link', { name: '← Voltar para a home' }).click();
    }
  }
  expect(apiCalls).toEqual([]);
});
