import type { Auth } from './models';

// A sessão permanece apenas em memória: recarregar a página exige novo login.
let session: Auth | null = null;
export type Mode = 'individual' | 'grupo';
let mode: Mode | null = null;
export function setMode(value: Mode | null) { mode = value; session = null; }
export function getMode() { return mode; }
export async function startIndividual(): Promise<Auth> {
  const local = await import('./local-db');
  await local.localRequest('/triagens');
  return { id: 1, nome: 'Triagem individual', email: 'Dados salvos só neste aparelho', token: '', expiraEm: '' };
}
export function setSession(value: Auth | null) { session = value; }

export async function request<T>(path: string, method = 'GET', body?: unknown, signal?: AbortSignal): Promise<T> {
  if (mode === 'individual') {
    const local = await import('./local-db');
    if (signal?.aborted) throw new DOMException('Aborted', 'AbortError');
    return local.localRequest(path, method, body) as Promise<T>;
  }
  if (mode !== 'grupo') throw new Error('Escolha um modo de triagem para continuar.');
  let response: Response;
  try {
    response = await fetch(`/api${path}`, {
      method, signal,
      headers: { ...(body ? { 'Content-Type': 'application/json' } : {}), ...(session ? { Authorization: `Bearer ${session.token}` } : {}) },
      body: body ? JSON.stringify(body) : undefined,
    });
  } catch (error) {
    if (signal?.aborted) throw error;
    throw new Error('Não foi possível conectar ao servidor. Verifique sua conexão e tente novamente.');
  }
  if (response.status === 401 && session) {
    setSession(null);
    window.dispatchEvent(new Event('session-expired'));
  }
  if (!response.ok) {
    const raw = await response.text();
    let message = raw;
    try {
      const problem = JSON.parse(raw);
      message = typeof problem === 'string' ? problem : Object.values(problem.errors ?? {}).flat().join(' ') || problem.detail || problem.title;
    } catch { /* Erros de validação da API também são texto simples. */ }
    throw new Error(message || `Não foi possível concluir a operação (${response.status}).`);
  }
  return response.status === 204 || response.headers.get('content-length') === '0' ? undefined as T :
    response.headers.get('content-type')?.includes('json') ? response.json() as Promise<T> : undefined as T;
}

export async function downloadExcel(id: number) {
  if (mode === 'individual') {
    const { localRequest } = await import('./local-db');
    const items = await localRequest(`/triagens/${id}/historico`);
    const { exportExcel } = await import('./local-excel');
    await exportExcel(items as import('./models').Historico[]);
    return;
  }
  const response = await fetch(`/api/triagens/${id}/historico/excel`, { headers: session ? { Authorization: `Bearer ${session.token}` } : {} });
  if (!response.ok) {
    if (response.status === 401) window.dispatchEvent(new Event('session-expired'));
    throw new Error('Não foi possível exportar o histórico.');
  }
  const url = URL.createObjectURL(await response.blob());
  const link = document.createElement('a');
  link.href = url; link.download = 'Triagens.xlsx'; link.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
