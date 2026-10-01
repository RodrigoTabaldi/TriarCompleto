import initSqlJs from 'sql.js';
import type { Database, SqlJsStatic } from 'sql.js';
import wasmUrl from 'sql.js/dist/sql-wasm.wasm?url';
import catalog from './default-triages.json';
import type { Detalhe, Faixa, Historico, ModeloInput, Resultado } from './models';

let engine: Promise<SqlJsStatic> | undefined;
let storage: Promise<IDBDatabase> | undefined;
let queue = Promise.resolve();

function openStorage() {
  return storage ??= new Promise((resolve, reject) => {
    const request = indexedDB.open('triar-individual-sqlite', 1);
    request.onupgradeneeded = () => request.result.createObjectStore('files');
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(new Error('Não foi possível abrir o armazenamento local. Verifique as permissões do navegador.'));
    request.onblocked = () => reject(new Error('Feche outras abas do Triar para abrir o banco local.'));
  });
}
async function readFile() {
  const store = await openStorage();
  return new Promise<Uint8Array | undefined>((resolve, reject) => {
    const tx = store.transaction('files', 'readonly');
    const request = tx.objectStore('files').get('triar.db');
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(new Error('Não foi possível ler o banco SQLite local.'));
  });
}
async function saveFile(bytes: Uint8Array) {
  const store = await openStorage();
  await new Promise<void>((resolve, reject) => {
    const tx = store.transaction('files', 'readwrite');
    tx.objectStore('files').put(bytes, 'triar.db');
    tx.oncomplete = () => resolve();
    tx.onabort = tx.onerror = () => reject(new Error('O navegador não conseguiu salvar o banco local. Libere espaço antes de tentar novamente.'));
  });
}
function rows(db: Database, sql: string, params: (string | number)[] = []) {
  const statement = db.prepare(sql);
  try { statement.bind(params); const result = []; while (statement.step()) result.push(statement.getAsObject()); return result; }
  finally { statement.free(); }
}
function details(db: Database, id: number): Detalhe {
  const row = rows(db, 'SELECT * FROM TriagemModelos WHERE Id=? AND Ativa=1', [id])[0];
  if (!row) throw new Error('Triagem não encontrada.');
  return {
    id, titulo: String(row.Titulo), publicoAlvo: String(row.PublicoAlvo), descricao: String(row.Descricao), icone: String(row.Icone),
    padrao: row.CriadorUsuarioId === null, criadorUsuarioId: row.CriadorUsuarioId === null ? null : Number(row.CriadorUsuarioId),
    perguntas: rows(db, 'SELECT * FROM Perguntas WHERE TriagemModeloId=? ORDER BY Ordem', [id]).map(p => ({ id: Number(p.Id), texto: String(p.Texto), peso: Number(p.Peso), ordem: Number(p.Ordem), categoria: String(p.Categoria), opcoes: JSON.parse(String(p.OpcoesJson)) })),
    faixas: rows(db, 'SELECT * FROM FaixasResultado WHERE TriagemModeloId=? ORDER BY Ordem', [id]).map(f => ({ titulo: String(f.Titulo), recomendacao: String(f.Recomendacao), pontuacaoMin: Number(f.PontuacaoMin), pontuacaoMax: Number(f.PontuacaoMax), cor: String(f.Cor) })),
  };
}
function insertContent(db: Database, id: number, model: ModeloInput) {
  model.perguntas.forEach((p, i) => {
    const question = p as Detalhe['perguntas'][number];
    db.run('INSERT INTO Perguntas(TriagemModeloId,Texto,Peso,Ordem,Categoria,OpcoesJson) VALUES(?,?,?,?,?,?)', [id, p.texto.trim(), p.peso, i + 1, question.categoria ?? '', JSON.stringify(question.opcoes ?? [])]);
  });
  [...model.faixas].sort((a, b) => a.pontuacaoMin - b.pontuacaoMin).forEach((f, i) => db.run('INSERT INTO FaixasResultado(TriagemModeloId,Titulo,Recomendacao,PontuacaoMin,PontuacaoMax,Cor,Ordem) VALUES(?,?,?,?,?,?,?)', [id, f.titulo.trim(), f.recomendacao.trim(), f.pontuacaoMin, f.pontuacaoMax, f.cor, i + 1]));
}
function seed(db: Database) {
  db.run(`
    CREATE TABLE Usuarios(Id INTEGER PRIMARY KEY,Nome TEXT NOT NULL,Email TEXT NOT NULL);
    CREATE TABLE TriagemModelos(Id INTEGER PRIMARY KEY AUTOINCREMENT,Titulo TEXT NOT NULL,PublicoAlvo TEXT NOT NULL,Descricao TEXT NOT NULL,Icone TEXT NOT NULL,CriadorUsuarioId INTEGER,Ativa INTEGER NOT NULL DEFAULT 1);
    CREATE TABLE Perguntas(Id INTEGER PRIMARY KEY AUTOINCREMENT,TriagemModeloId INTEGER NOT NULL,Texto TEXT NOT NULL,Peso INTEGER NOT NULL,Ordem INTEGER NOT NULL,Categoria TEXT NOT NULL,OpcoesJson TEXT NOT NULL);
    CREATE TABLE FaixasResultado(Id INTEGER PRIMARY KEY AUTOINCREMENT,TriagemModeloId INTEGER NOT NULL,Titulo TEXT NOT NULL,Recomendacao TEXT NOT NULL,PontuacaoMin INTEGER NOT NULL,PontuacaoMax INTEGER NOT NULL,Cor TEXT NOT NULL,Ordem INTEGER NOT NULL);
    CREATE TABLE UsuarioTriagensHome(UsuarioId INTEGER NOT NULL,TriagemModeloId INTEGER NOT NULL,Visivel INTEGER NOT NULL,Ordem INTEGER NOT NULL,PRIMARY KEY(UsuarioId,TriagemModeloId));
    CREATE TABLE TriagemResultados(Id INTEGER PRIMARY KEY AUTOINCREMENT,TriagemModeloId INTEGER NOT NULL,UsuarioId INTEGER NOT NULL,DadosJson TEXT NOT NULL);
    CREATE TABLE RespostasDadas(Id INTEGER PRIMARY KEY AUTOINCREMENT,TriagemResultadoId INTEGER NOT NULL,PerguntaId INTEGER NOT NULL,Valor INTEGER NOT NULL,OpcoesSelecionadasJson TEXT NOT NULL);
    INSERT INTO Usuarios VALUES(1,'Triagem individual','');
    PRAGMA user_version=1;
  `);
  for (const model of catalog) {
    db.run('INSERT INTO TriagemModelos(Id,Titulo,PublicoAlvo,Descricao,Icone) VALUES(?,?,?,?,?)', [model.id, model.titulo, model.publicoAlvo, model.descricao, model.icone]);
    insertContent(db, model.id, model);
  }
}
function validate(model: ModeloInput) {
  if (!model?.titulo?.trim() || model.titulo.length > 150) throw new Error('Informe um título de até 150 caracteres.');
  if (!model.perguntas?.length || model.perguntas.length > 50 || model.perguntas.some(p => !p.texto.trim() || p.texto.length > 500 || !Number.isInteger(p.peso) || p.peso < 1 || p.peso > 100)) throw new Error('Defina de 1 a 50 perguntas com texto e peso inteiro entre 1 e 100.');
  if (model.faixas.length < 2) throw new Error('Defina pelo menos duas faixas de resultado.');
  const sorted = [...model.faixas].sort((a, b) => a.pontuacaoMin - b.pontuacaoMin);
  const total = model.perguntas.reduce((sum, p) => sum + p.peso, 0);
  if (sorted[0].pontuacaoMin !== 0 || sorted.at(-1)!.pontuacaoMax < total) throw new Error('As faixas devem cobrir de zero até a pontuação máxima.');
  for (let i = 0; i < sorted.length; i++) {
    const f = sorted[i];
    if (!f.titulo.trim() || !Number.isInteger(f.pontuacaoMin) || !Number.isInteger(f.pontuacaoMax) || f.pontuacaoMin > f.pontuacaoMax || !/^#[0-9a-f]{6}$/i.test(f.cor)) throw new Error('Confira os títulos, intervalos e cores das faixas.');
    if (i > 0 && f.pontuacaoMin <= sorted[i - 1].pontuacaoMax) throw new Error('As faixas não podem se sobrepor.');
  }
}
function dispatch(db: Database, path: string, method: string, body: unknown): unknown {
  if (path === '/triagens' && method === 'GET') return rows(db, 'SELECT Id FROM TriagemModelos WHERE Ativa=1 ORDER BY CriadorUsuarioId IS NOT NULL,Id').map(row => {
    const model = details(db, Number(row.Id));
    const pref = rows(db, 'SELECT Visivel FROM UsuarioTriagensHome WHERE UsuarioId=1 AND TriagemModeloId=?', [model.id])[0];
    return { ...model, minhaAutoria: !model.padrao, visivelNaHome: pref ? Boolean(pref.Visivel) : true, totalPerguntas: model.perguntas.length };
  });
  if (/^\/usuarios\/1\/home$/.test(path) && method === 'PUT') {
    const input = body as { itens: { triagemModeloId: number; visivel: boolean; ordem: number }[] };
    for (const item of input.itens) {
      details(db, item.triagemModeloId);
      db.run('INSERT OR REPLACE INTO UsuarioTriagensHome VALUES(1,?,?,?)', [item.triagemModeloId, Number(item.visivel), item.ordem]);
    }
    return undefined;
  }
  const match = /^\/triagens\/(\d+)(?:\/(responder|historico))?$/.exec(path);
  const legacy = /^\/triagem\/usuario\/1(?:\?triagemModeloId=(\d+))?$/.exec(path);
  if (legacy || match?.[2] === 'historico') {
    const id = match ? Number(match[1]) : legacy?.[1] ? Number(legacy[1]) : undefined;
    return rows(db, `SELECT Id,DadosJson FROM TriagemResultados WHERE UsuarioId=1${id ? ' AND TriagemModeloId=?' : ''} ORDER BY Id DESC`, id ? [id] : []).map(row => {
      const result = JSON.parse(String(row.DadosJson)) as Resultado;
      return { ...result, id: Number(row.Id), nome: result.nomePaciente, resultado: result.classificacao };
    });
  }
  if (path === '/triagens' && method === 'POST') {
    const model = body as ModeloInput; validate(model);
    db.run('INSERT INTO TriagemModelos(Titulo,PublicoAlvo,Descricao,Icone,CriadorUsuarioId) VALUES(?,?,?,?,1)', [model.titulo.trim(), model.publicoAlvo.trim() || 'Todas as idades', model.descricao.trim(), model.icone || '📋']);
    const id = Number(rows(db, 'SELECT last_insert_rowid() AS Id')[0].Id);
    insertContent(db, id, model); return details(db, id);
  }
  if (!match) throw new Error('Operação indisponível no modo individual.');
  const id = Number(match[1]);
  const model = details(db, id);
  if (method === 'GET') return model;
  if (match[2] === 'responder' && method === 'POST') {
    const input = body as { nomePaciente: string; idade: number; sexo: string; escolaridade?: string; doencasPrevias?: string; respostas: { perguntaId: number; valor: boolean; opcoesSelecionadas?: string[] }[] };
    if (!input.nomePaciente?.trim() || !Number.isInteger(input.idade) || input.idade < 0 || input.idade > 130) throw new Error('Confira o nome e a idade da pessoa avaliada.');
    if (input.respostas.length !== model.perguntas.length || new Set(input.respostas.map(r => r.perguntaId)).size !== model.perguntas.length) throw new Error('Responda cada pergunta exatamente uma vez.');
    let score = 0;
    for (const answer of input.respostas) {
      const question = model.perguntas.find(p => p.id === answer.perguntaId);
      if (!question) throw new Error('Pergunta inválida.');
      const selected = answer.opcoesSelecionadas ?? [];
      if (new Set(selected).size !== selected.length || selected.some(o => !question.opcoes?.includes(o))) throw new Error('Opção de resposta inválida.');
      score += question.opcoes?.length ? selected.length * question.peso : answer.valor ? question.peso : 0;
    }
    const max = model.perguntas.reduce((sum, p) => sum + p.peso * Math.max(1, p.opcoes?.length ?? 0), 0);
    const range = model.faixas.find(f => score >= f.pontuacaoMin && score <= f.pontuacaoMax) ?? model.faixas.at(-1) as Faixa;
    const result: Resultado = { id: 0, triagemModeloId: id, tituloTriagem: model.titulo, nomePaciente: input.nomePaciente.trim(), idade: input.idade, sexo: input.sexo, escolaridade: input.escolaridade ?? '', doencasPrevias: input.doencasPrevias ?? '', pontuacao: score, pontuacaoMaxima: max, classificacao: range.titulo, recomendacao: range.recomendacao, cor: range.cor, data: new Date().toISOString() };
    db.run('INSERT INTO TriagemResultados(TriagemModeloId,UsuarioId,DadosJson) VALUES(?,1,?)', [id, JSON.stringify(result)]);
    result.id = Number(rows(db, 'SELECT last_insert_rowid() AS Id')[0].Id);
    for (const answer of input.respostas) db.run('INSERT INTO RespostasDadas(TriagemResultadoId,PerguntaId,Valor,OpcoesSelecionadasJson) VALUES(?,?,?,?)', [result.id, answer.perguntaId, Number(answer.valor), JSON.stringify(answer.opcoesSelecionadas ?? [])]);
    return result;
  }
  if (model.padrao) throw new Error('Apenas triagens de sua autoria podem ser alteradas.');
  if (method === 'DELETE') { db.run('UPDATE TriagemModelos SET Ativa=0 WHERE Id=?', [id]); return undefined; }
  if (method === 'PUT') {
    const input = body as ModeloInput; validate(input);
    db.run('UPDATE TriagemModelos SET Titulo=?,PublicoAlvo=?,Descricao=?,Icone=? WHERE Id=?', [input.titulo.trim(), input.publicoAlvo.trim(), input.descricao.trim(), input.icone, id]);
    db.run('DELETE FROM Perguntas WHERE TriagemModeloId=?', [id]); db.run('DELETE FROM FaixasResultado WHERE TriagemModeloId=?', [id]);
    insertContent(db, id, input); return undefined;
  }
  throw new Error('Operação indisponível.');
}
async function execute(path: string, method: string, body: unknown) {
  const SQL = await (engine ??= initSqlJs({ locateFile: () => wasmUrl }));
  const file = await readFile();
  const db = new SQL.Database(file);
  try {
    db.run('BEGIN');
    if (!file) seed(db);
    const result = dispatch(db, path, method, body);
    db.run('COMMIT');
    // Só confirma a ação depois que a gravação persistente foi concluída.
    if (!file || method !== 'GET') await saveFile(db.export());
    return result;
  } finally { db.close(); }
}
export function localRequest(path: string, method = 'GET', body?: unknown): Promise<unknown> {
  const action = () => navigator.locks ? navigator.locks.request('triar-individual-sqlite', () => execute(path, method, body)) : execute(path, method, body);
  const result = queue.then(action);
  queue = result.then(() => undefined, () => undefined);
  return result;
}
export async function exportSqlite() {
  await localRequest('/triagens');
  const bytes = await readFile();
  if (!bytes) throw new Error('Banco local não encontrado.');
  const url = URL.createObjectURL(new Blob([bytes.slice().buffer], { type: 'application/x-sqlite3' }));
  const link = document.createElement('a'); link.href = url; link.download = 'Triar-individual.sqlite'; link.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
export async function individualHistory(): Promise<Historico[]> { return localRequest('/triagem/usuario/1') as Promise<Historico[]>; }
