import { useEffect, useState } from 'react';
import { Link, NavLink } from 'react-router-dom';
import { getMode, request } from './api';
import type { Auth, Resumo } from './models';
import { triageImage } from './triage-image';

const menu = [
  { path: '/', icon: 'home', label: 'Início' },
  { path: '/historico', icon: 'history', label: 'Histórico geral' },
  { path: '/creditos', icon: 'credits', label: 'Créditos' },
  { path: '/contato', icon: 'contact', label: 'Contato' },
  { path: '/sobre', icon: 'about', label: 'Sobre o projeto' },
  { path: '/triagens/nova', icon: 'create', label: 'Criar sua triagem' },
];
export function Navigation({ logout }: { logout: () => void }) {
  return <><aside className="sidebar"><img src="/assets/logo.png" alt="Triar" className="sidebar-logo" /><nav>{menu.map((item, i) => <NavLink end key={item.path} className={i === 2 ? 'nav-divider' : ''} to={item.path}><img src={`/assets/nav_${item.icon}.svg`} alt="" />{item.label}</NavLink>)}</nav><button className="nav-logout" onClick={logout}><img src="/assets/nav_logout.svg" alt="" />{getMode() === 'individual' ? 'Trocar modo de triagem' : 'Sair'}</button></aside>
    <nav className="bottom-nav" aria-label="Navegação principal">{menu.filter(item => item.icon !== 'history').map(item => <NavLink end key={item.path} to={item.path}><img src={`/assets/nav_${item.icon}.svg`} alt="" /><span>{item.icon === 'create' ? 'Criar triagem' : item.label}</span></NavLink>)}</nav></>;
}
export function Home({ user, logout }: { user: Auth; logout: () => void }) {
  const [items, setItems] = useState<Resumo[] | null>(null);
  const [revision, setRevision] = useState(0);
  const [error, setError] = useState('');
  const [editing, setEditing] = useState(false);
  const [busy, setBusy] = useState(false);
  const [remove, setRemove] = useState<Resumo | null>(null);
  const individual = getMode() === 'individual';
  const initials = individual ? 'TI' : user.nome.trim().split(/\s+/).slice(0, 2).map(s => s[0]).join('').toUpperCase();
  useEffect(() => {
    const controller = new AbortController();
    request<Resumo[]>('/triagens', 'GET', undefined, controller.signal).then(setItems).catch(e => { if (!controller.signal.aborted) setError(e.message); });
    return () => controller.abort();
  }, [revision]);
  function reload() { if (!editing) { setError(''); setRevision(x => x + 1); } }
  async function save() {
    setBusy(true); setError('');
    try { await request(`/usuarios/${user.id}/home`, 'PUT', { itens: items?.map((t, ordem) => ({ triagemModeloId: t.id, visivel: t.visivelNaHome, ordem })) }); setEditing(false); setRevision(x => x + 1); }
    catch (e) { setError(e instanceof Error ? e.message : 'Não foi possível salvar.'); } finally { setBusy(false); }
  }
  async function deleteModel() {
    if (!remove) return;
    setBusy(true); setError('');
    try { await request(`/triagens/${remove.id}`, 'DELETE'); setItems(old => old!.filter(t => t.id !== remove.id)); setRemove(null); }
    catch (e) { setError(e instanceof Error ? e.message : 'Não foi possível excluir.'); } finally { setBusy(false); }
  }
  return <div className="home-layout"><Navigation logout={logout} /><main className="home-main">
    <header className="home-header"><img className="mobile-logo" src="/assets/logo.png" alt="Triar" /><span className="home-avatar">{initials}</span><div className="desktop-identity"><strong>{individual ? 'Triagem individual' : user.nome}</strong><small>{individual ? 'Dados salvos só neste aparelho' : user.email}</small></div><div className="desktop-actions"><button className="outline" onClick={() => { if (editing) { setEditing(false); setRevision(x => x + 1); } else setEditing(true); }}>{editing ? 'Cancelar edição' : '✎  Editar triagens'}</button><button className="neutral" disabled={editing} onClick={reload}>↻  Atualizar</button></div></header>
    <section className="mobile-greeting"><h2>{individual ? 'Olá' : `Olá, ${user.nome}`}</h2><p>Como você está hoje?</p></section>
    <section className="home-title"><h1>Triagens disponíveis</h1><p className="desktop-subtitle">Selecione uma triagem para iniciar o atendimento</p><p className="mobile-subtitle">Escolha uma triagem para iniciar</p></section>
    {error && <div className="error" role="alert">{error}</div>}{!items && !error && <p role="status">Carregando triagens…</p>}
    <div className="triage-grid">{items?.filter(t => editing || t.visivelNaHome).map(t => <article className="triage-card" key={t.id}><div className="triage-cover"><img src={triageImage(t.titulo)} alt="" /></div><div className="triage-info"><h2>{t.titulo}</h2><p className="triage-audience"><span>Público alvo:</span>{t.publicoAlvo}</p><div className="triage-buttons"><Link className="button" to={`/triagens/${t.id}`}>Entrar</Link><Link className="button outline" to={`/triagens/${t.id}/historico`}><span className="desktop-history">Ver histórico de pacientes</span><span className="mobile-history">Ver histórico</span></Link></div>
      {editing && <div className="triage-edit"><label className="check"><input type="checkbox" checked={t.visivelNaHome} onChange={e => setItems(old => old!.map(item => item.id === t.id ? { ...item, visivelNaHome: e.target.checked } : item))} />Exibir na home</label>{t.minhaAutoria && <><Link aria-label={`Editar ${t.titulo}`} className="button soft" to={`/triagens/${t.id}/editar`}>✎</Link><button aria-label={`Excluir ${t.titulo}`} className="danger" onClick={() => setRemove(t)}>🗑</button></>}</div>}
    </div></article>)}</div>
    {items && items.every(t => !t.visivelNaHome) && !editing && <p>Nenhuma triagem visível. Use “Editar triagens” para escolher as triagens.</p>}
    {editing && <button className="save" disabled={busy} onClick={save}>Salvar configuração da home</button>}
    <footer className="home-footer"><div><strong>Segurança</strong><p>Acesso protegido aos seus dados.</p></div><div><strong>Privacidade</strong><p>{individual ? 'Dados salvos somente neste navegador.' : 'Histórico separado por usuário.'}</p></div><div><strong>Uso Acadêmico</strong><p>Projeto para fins acadêmicos e aprendizado.</p></div><div><strong>Melhoria Contínua</strong><p>Sua participação ajuda a melhorar o sistema.</p></div></footer>
    <div className="mobile-tools"><button className="text" onClick={() => setEditing(x => !x)}>{editing ? 'Cancelar edição' : '✎ Editar triagens'}</button><button className="text" disabled={editing} onClick={reload}>↻ Atualizar</button><Link to="/historico">Histórico geral</Link><button className="text" onClick={logout}>Trocar modo de triagem</button></div>
    {remove && <div className="modal-backdrop"><section className="card modal" role="dialog" aria-modal="true" aria-labelledby="delete-title"><h2 id="delete-title">Excluir triagem?</h2><p>{remove.titulo} será removida da home. O histórico será preservado.</p><div className="actions"><button disabled={busy} className="outline" autoFocus onClick={() => setRemove(null)}>Cancelar</button><button disabled={busy} className="danger" onClick={deleteModel}>Confirmar exclusão</button></div></section></div>}
  </main></div>;
}
