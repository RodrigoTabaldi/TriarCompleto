import { createContext, useContext, useEffect, useState } from 'react';
import type { FormEvent, ReactNode } from 'react';
import { Link, Navigate, Route, Routes, useLocation, useNavigate, useParams } from 'react-router-dom';
import { downloadExcel, request, setSession, setMode, startIndividual, getMode } from './api';
import type { Mode } from './api';
import { ModeChoice } from './ModeChoice';
import { Home } from './Home';
import { triageImage } from './triage-image';
import type { Auth, Detalhe, Historico, ModeloInput, Resultado } from './models';

const Session = createContext<Auth | null>(null);
const message = (error: unknown) => error instanceof Error ? error.message : 'Não foi possível concluir a operação.';
const color = (value: string) => /^#[0-9a-f]{6}$/i.test(value) ? value : '#10B981';
const date = (value: string) => new Date(/Z$|[+-]\d\d:\d\d$/.test(value) ? value : `${value}Z`).toLocaleString('pt-BR');
function Logo() { return <img className="logo" src="/assets/logo.png" alt="Triar" />; }
function ErrorNotice({ error }: { error: string }) { return error ? <div className="error" role="alert">{error}</div> : null; }
function Page({ title, children }: { title: string; children: ReactNode }) {
  return <main className="page narrow"><Link className="back" to="/">← Voltar para a home</Link><div className="center"><Logo /></div><h1>{title}</h1>{children}</main>;
}
function useLoad<T>(path: string) {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState('');
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    setData(null); setError('');
    request<T>(path, 'GET', undefined, controller.signal).then(setData).catch(e => { if (!controller.signal.aborted) setError(message(e)); });
    return () => controller.abort();
  }, [path, revision]);
  return { data, setData, error, setError, reload: () => setRevision(x => x + 1) };
}

export function App() {
  const [user, setUser] = useState<Auth | null>(null);
  const [mode, selectMode] = useState<Mode | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const navigate = useNavigate();
  const location = useLocation();
  function logout() { setMode(null); selectMode(null); setUser(null); setError(''); navigate('/'); }
  async function choose(value: Mode) {
    setBusy(true); setError(''); setMode(value);
    try {
      if (value === 'individual') { setUser(await startIndividual()); navigate('/'); }
      else navigate('/login');
      selectMode(value);
    } catch (e) { setMode(null); setError(message(e)); }
    finally { setBusy(false); }
  }
  useEffect(() => {
    const expire = () => { setSession(null); setUser(null); navigate('/login', { state: { expired: true } }); };
    window.addEventListener('session-expired', expire);
    return () => window.removeEventListener('session-expired', expire);
  }, [navigate]);
  useEffect(() => { window.scrollTo(0, 0); }, [location.pathname]);
  function authenticate(value: Auth) { setSession(value); setUser(value); navigate('/'); }
  if (!mode) return <ModeChoice onChoose={choose} busy={busy} error={error} />;
  if (!user) return <Routes><Route path="/login" element={<AuthPage onAuth={authenticate} onBack={logout} />} /><Route path="/cadastro" element={<AuthPage register onAuth={authenticate} onBack={logout} />} /><Route path="*" element={<Navigate to="/login" replace />} /></Routes>;
  return <Session.Provider value={user}><Routes>
    <Route path="/" element={<Home user={user} logout={logout} />} />
    <Route path="/triagens/nova" element={<Editor />} />
    <Route path="/triagens/:id/editar" element={<EditLoader />} />
    <Route path="/triagens/:id" element={<ScreeningLoader />} />
    <Route path="/triagens/:id/historico" element={<History />} />
    <Route path="/historico" element={<History />} />
    <Route path="/creditos" element={<Credits />} />
    <Route path="/contato" element={<Contact />} />
    <Route path="/resultado" element={<Result />} />
    <Route path="/sobre" element={<About />} />
    <Route path="*" element={<Navigate to="/" replace />} />
  </Routes></Session.Provider>;
}

function AuthPage({ register = false, onAuth, onBack }: { register?: boolean; onAuth: (user: Auth) => void; onBack: () => void }) {
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const location = useLocation();
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true); setError('');
    const form = new FormData(event.currentTarget);
    try {
      if (register && form.get('senha') !== form.get('confirmacao')) throw new Error('As senhas não coincidem.');
      onAuth(await request<Auth>(`/auth/${register ? 'register' : 'login'}`, 'POST', { nome: form.get('nome'), email: form.get('email'), senha: form.get('senha') }));
    } catch (e) { setError(message(e)); } finally { setBusy(false); }
  }
  return <main className="auth"><img className="watermark" src="/assets/iconelogo.png" alt="" /><form className="card auth-card" onSubmit={submit}>
    <Logo /><h1>{register ? 'Crie sua conta' : 'Bem-vindo(a) de volta!'}</h1><p className="muted center">{register ? 'Cadastre-se para começar a usar o Triar' : 'Faça login para acessar sua conta'}</p>
    {location.state?.expired && <p role="status">Sua sessão expirou. Entre novamente.</p>}<ErrorNotice error={error} />
    {register && <label>Nome completo<input name="nome" required maxLength={120} autoComplete="name" placeholder="Seu nome" /></label>}
    <label>Email<input name="email" required type="email" maxLength={180} autoComplete="email" placeholder="Email" /></label>
    <label>Senha<input name="senha" required type="password" minLength={register ? 8 : undefined} autoComplete={register ? 'new-password' : 'current-password'} placeholder={register ? 'Mínimo de 8 caracteres' : 'Sua senha'} /></label>
    {register && <label>Confirmar senha<input name="confirmacao" required type="password" autoComplete="new-password" placeholder="Repita sua senha" /></label>}
    <button disabled={busy}>{busy ? 'Aguarde…' : register ? 'Cadastrar' : 'Entrar'}</button><span className="muted center">ou</span>
    <Link className="button outline" to={register ? '/login' : '/cadastro'}>{register ? 'Já tenho uma conta' : 'Cadastrar'}</Link>
    <button className="text auth-back" type="button" onClick={onBack}>← Voltar</button>
  </form></main>;
}

function ScreeningLoader() {
  const { id } = useParams();
  const { data, error, reload } = useLoad<Detalhe>(`/triagens/${id}`);
  return data ? <Screening key={data.id} model={data} /> : <Page title="Triagem"><ErrorNotice error={error} />{error ? <button onClick={reload}>Tentar novamente</button> : <p role="status">Carregando perguntas…</p>}</Page>;
}
function Screening({ model }: { model: Detalhe }) {
  const [answers, setAnswers] = useState<Record<number, boolean>>(() => Object.fromEntries(model.perguntas.filter(p => p.opcoes?.length).map(p => [p.id, false])));
  const [selected, setSelected] = useState<Record<number, string[]>>({});
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const navigate = useNavigate();
  const count = Object.keys(answers).length;
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setError('');
    if (count !== model.perguntas.length) { setError('Responda todas as perguntas antes de finalizar.'); return; }
    const form = new FormData(event.currentTarget); setBusy(true);
    try {
      const result = await request<Resultado>(`/triagens/${model.id}/responder`, 'POST', { nomePaciente: form.get('nome'), idade: Number(form.get('idade')), sexo: form.get('sexo'), escolaridade: form.get('escolaridade'), doencasPrevias: form.get('doencasPrevias'), respostas: model.perguntas.map(p => ({ perguntaId: p.id, valor: answers[p.id], opcoesSelecionadas: selected[p.id] ?? [] })) });
      navigate('/resultado', { state: { result } });
    } catch (e) { setError(message(e)); } finally { setBusy(false); }
  }
  return <Page title={model.titulo}><form onSubmit={submit} className="stack"><section className="card"><div className="card-heading"><img className="screening-cover" src={triageImage(model.titulo)} alt="" /><div className="grow"><p>Pergunta {count} de {model.perguntas.length}</p><progress value={count} max={model.perguntas.length} aria-label="Progresso da triagem" /> <small>{Math.round(count / model.perguntas.length * 100)}%</small></div></div></section>
    <section className="card stack"><h2>Dados iniciais</h2><label>Nome completo<input name="nome" required maxLength={150} placeholder="Digite o nome" /></label><label>Escolaridade<select name="escolaridade" required defaultValue=""><option value="" disabled>Selecione</option>{['Ensino fundamental incompleto','Ensino fundamental completo','Ensino médio incompleto','Ensino médio completo','Ensino superior incompleto','Ensino superior completo','Pós-graduação incompleta','Pós-graduação completa'].map(value => <option key={value}>{value}</option>)}</select></label><label>Doenças prévias (opcional)<input name="doencasPrevias" maxLength={600} placeholder="Informe se houver" /></label><div className="fields"><label>Idade<input name="idade" type="number" required min={0} max={130} step={1} placeholder="Digite a idade" /></label><label>Sexo<select name="sexo" required defaultValue=""><option value="" disabled>Selecione</option><option>Feminino</option><option>Masculino</option><option>Outro</option><option>Prefiro não informar</option></select></label></div></section><h2>Perguntas da triagem</h2>
    {model.perguntas.map((p, i) => <fieldset className="card question" key={p.id}><legend>{p.categoria && <span className="category">{p.categoria}</span>}<span className="question-title"><span className="question-number">{i + 1}</span>{p.texto.replace(/^\d+\.\s*/, '')}</span></legend>{p.opcoes?.length ? <div className="stack option-list">{p.opcoes.map(option => <label className="check" key={option}><input type="checkbox" checked={selected[p.id]?.includes(option) ?? false} onChange={e => setSelected(old => ({ ...old, [p.id]: e.target.checked ? [...(old[p.id] ?? []), option] : (old[p.id] ?? []).filter(value => value !== option) }))} />{option}</label>)}<small>Selecione as opções relatadas, ou deixe desmarcadas se nenhuma se aplica.</small></div> : <div className="answers">{[true, false].map(value => <label key={String(value)} className={answers[p.id] === value ? 'selected' : ''}><input required type="radio" name={`question-${p.id}`} checked={answers[p.id] === value} onChange={() => setAnswers(old => ({ ...old, [p.id]: value }))} />{value ? 'Sim' : 'Não'}</label>)}</div>}</fieldset>)}
    <ErrorNotice error={error} /><button disabled={busy}>{busy ? 'Calculando…' : 'Finalizar triagem ✓'}</button></form></Page>;
}
function Result() {
  const location = useLocation();
  const result = location.state?.result as Resultado | undefined;
  if (!result) return <Navigate to="/" replace />;
  return <Page title="Resultado da triagem"><p className="result-subtitle">{result.tituloTriagem}</p><section className="card result"><div className="score-circle" style={{ backgroundColor: color(result.cor) }}><strong>{result.pontuacao}</strong><span>de {result.pontuacaoMaxima} pontos</span></div><h2 style={{ color: color(result.cor) }}>{result.classificacao}</h2><p>{result.recomendacao}</p><div className="result-data"><div><small>Paciente</small><strong>{result.nomePaciente}</strong></div><div><small>Idade</small><strong>{result.idade} anos</strong></div><div><small>Data</small><strong>{date(result.data)}</strong></div></div><div className="result-data extra"><div><small>Escolaridade</small><strong>{result.escolaridade || 'Não informada'}</strong></div><div><small>Doenças prévias</small><strong>{result.doencasPrevias || 'Não informadas'}</strong></div></div></section><p className="clinical-notice">Protótipo acadêmico não homologado clinicamente. A pontuação é educativa e não deve orientar diagnóstico, tratamento ou decisões de urgência. Procure um profissional habilitado se houver preocupação.</p><div className="stack"><Link className="button" to={`/triagens/${result.triagemModeloId}`}>🔁 Aplicar esta triagem em outra pessoa</Link><Link className="button outline" to={`/triagens/${result.triagemModeloId}/historico`}>Ver histórico desta triagem</Link></div></Page>;
}
function History() {
  const { id } = useParams();
  const user = useContext(Session)!;
  const { data, error, setError, reload } = useLoad<Historico[]>(id ? `/triagens/${id}/historico` : `/triagem/usuario/${user.id}`);
  const [busy, setBusy] = useState(false);
  async function exportFile() { setBusy(true); try { await downloadExcel(Number(id ?? 0)); } catch (e) { setError(message(e)); } finally { setBusy(false); } }
  return <Page title="Histórico de pacientes"><div className="actions"><button disabled={busy || !data?.length} onClick={exportFile}>Exportar Excel</button><button className="outline" onClick={reload}>↻ Atualizar</button></div><ErrorNotice error={error} />{!data && !error && <p role="status">Carregando histórico…</p>}{data?.length === 0 && <p className="card">Nenhuma triagem realizada ainda.</p>}<div className="stack">{data?.map(item => <article className="card history-item" key={item.id}><div><h2>{item.nome}</h2><p>{item.tituloTriagem}</p><small>{item.idade} anos · {item.sexo} · {date(item.data)}</small></div><div><strong style={{ color: color(item.cor) }}>{item.resultado}</strong><p>{item.pontuacao}/{item.pontuacaoMaxima} pontos</p></div></article>)}</div></Page>;
}

function Credits() {
  return <Page title="Créditos"><p className="center muted">Equipe responsável pelo projeto</p><div className="stack credits">{[['Gabriela de Luccia Dutra','Orientadora principal e responsável pela área de Fonoaudiologia'],['Rodrigo Tabaldi','Desenvolvedor principal do software'],['Waine Teixeira Junior','Supervisor de Engenharia de Software'],['Yasmin Paes Ferreira','Colaboradora discente em Fonoaudiologia']].map(([name, role]) => <article className="card" key={name}><h2>{name}</h2><p>{role}</p></article>)}</div><p className="notice center">Projeto acadêmico da Universidade Federal de Rondonópolis (UFR).</p></Page>;
}
function Contact() {
  return <Page title="Contato"><section className="card stack"><h2>Dúvidas sobre o projeto</h2><strong>Triar</strong><a href="mailto:triarcontato@gmail.com">triarcontato@gmail.com</a><strong>Desenvolvedor principal</strong><a href="mailto:r.tabaldi@aluno.ufr.edu.br">r.tabaldi@aluno.ufr.edu.br</a><strong>Orientadora do projeto</strong><a href="mailto:gabriela.dutra@ufr.com.br">gabriela.dutra@ufr.com.br</a><a className="button" href="mailto:triarcontato@gmail.com">Enviar e-mail</a></section><p className="notice">O aplicativo não realiza atendimento clínico. Em caso de sintomas vocais persistentes, procure um fonoaudiólogo ou outro serviço de saúde.</p></Page>;
}

function EditLoader() {
  const { id } = useParams();
  const user = useContext(Session)!;
  const { data, error, reload } = useLoad<Detalhe>(`/triagens/${id}`);
  if (data && data.criadorUsuarioId !== user.id) return <Page title="Editar triagem"><ErrorNotice error="Apenas o criador pode editar esta triagem." /></Page>;
  return data ? <Editor initial={data} key={data.id} /> : <Page title="Editar triagem"><ErrorNotice error={error} />{error ? <button onClick={reload}>Tentar novamente</button> : <p role="status">Carregando…</p>}</Page>;
}
const emptyModel: ModeloInput = {
  titulo: '', publicoAlvo: '', descricao: '', icone: '📋', perguntas: [{ texto: '', peso: 1 }],
  faixas: [{ titulo: 'Baixo risco', pontuacaoMin: 0, pontuacaoMax: 0, recomendacao: 'Sem sinais de alerta no momento.', cor: '#10B981' }, { titulo: 'Alto risco', pontuacaoMin: 1, pontuacaoMax: 1, recomendacao: 'Procure uma avaliação profissional.', cor: '#EF4444' }],
};
function Editor({ initial }: { initial?: Detalhe }) {
  const [model, setModel] = useState<ModeloInput>(initial ?? emptyModel);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const navigate = useNavigate();
  const total = model.perguntas.reduce((sum, p) => sum + p.peso, 0);
  function field(key: 'titulo' | 'publicoAlvo' | 'descricao' | 'icone', value: string) { setModel(old => ({ ...old, [key]: value })); }
  async function submit(e: FormEvent) {
    e.preventDefault(); setError('');
    if (model.perguntas.length === 0 || model.faixas.length < 2) { setError('Adicione pelo menos uma pergunta e duas faixas de resultado.'); return; }
    setBusy(true);
    try { await request(initial ? `/triagens/${initial.id}` : '/triagens', initial ? 'PUT' : 'POST', model); navigate('/'); }
    catch (e) { setError(message(e)); } finally { setBusy(false); }
  }
  return <Page title={initial ? 'Editar triagem' : 'Criar sua triagem'}><form className="stack" onSubmit={submit}><section className="card stack"><label>Título<input required value={model.titulo} maxLength={150} onChange={e => field('titulo', e.target.value)} /></label><label>Público alvo<input value={model.publicoAlvo} maxLength={150} onChange={e => field('publicoAlvo', e.target.value)} /></label><label>Descrição<textarea value={model.descricao} maxLength={600} onChange={e => field('descricao', e.target.value)} /></label><label>Ícone<input value={model.icone} maxLength={16} onChange={e => field('icone', e.target.value)} /></label></section>
    <h2>Perguntas (sim / não)</h2><p className="muted">Pontuação máxima possível: {total}</p>{model.perguntas.map((p, i) => <section className="card stack" key={i}><label>Pergunta {i + 1}<input required maxLength={500} value={p.texto} onChange={e => setModel(old => ({ ...old, perguntas: old.perguntas.map((item, n) => n === i ? { ...item, texto: e.target.value } : item) }))} /></label><label>Peso<input required type="number" min={1} max={100} step={1} value={p.peso} onChange={e => setModel(old => ({ ...old, perguntas: old.perguntas.map((item, n) => n === i ? { ...item, peso: Number(e.target.value) } : item) }))} /></label><button className="text danger-text" type="button" onClick={() => setModel(old => ({ ...old, perguntas: old.perguntas.filter((_, n) => n !== i) }))}>Remover pergunta {i + 1}</button></section>)}
    <button className="outline" type="button" disabled={model.perguntas.length >= 50} onClick={() => setModel(old => ({ ...old, perguntas: [...old.perguntas, { texto: '', peso: 1 }] }))}>＋ Adicionar pergunta</button>
    <h2>Faixas de resultado (metas)</h2>{model.faixas.map((f, i) => <section className="card stack" key={i}><label>Título da faixa {i + 1}<input required maxLength={120} value={f.titulo} onChange={e => setModel(old => ({ ...old, faixas: old.faixas.map((item, n) => n === i ? { ...item, titulo: e.target.value } : item) }))} /></label><div className="fields">{(['pontuacaoMin', 'pontuacaoMax'] as const).map(key => <label key={key}>{key === 'pontuacaoMin' ? 'Mínimo' : 'Máximo'}<input required type="number" min={0} step={1} value={f[key]} onChange={e => setModel(old => ({ ...old, faixas: old.faixas.map((item, n) => n === i ? { ...item, [key]: Number(e.target.value) } : item) }))} /></label>)}</div><label>Recomendação<textarea maxLength={600} value={f.recomendacao} onChange={e => setModel(old => ({ ...old, faixas: old.faixas.map((item, n) => n === i ? { ...item, recomendacao: e.target.value } : item) }))} /></label><label>Cor<input type="color" value={color(f.cor)} onChange={e => setModel(old => ({ ...old, faixas: old.faixas.map((item, n) => n === i ? { ...item, cor: e.target.value } : item) }))} /></label><button type="button" className="text danger-text" onClick={() => setModel(old => ({ ...old, faixas: old.faixas.filter((_, n) => n !== i) }))}>Remover faixa {i + 1}</button></section>)}
    <button className="outline" type="button" onClick={() => setModel(old => ({ ...old, faixas: [...old.faixas, { titulo: '', pontuacaoMin: (old.faixas.at(-1)?.pontuacaoMax ?? -1) + 1, pontuacaoMax: total, recomendacao: '', cor: '#F59E0B' }] }))}>＋ Adicionar faixa</button><ErrorNotice error={error} /><button disabled={busy}>{busy ? 'Salvando…' : initial ? 'Salvar alterações ✓' : 'Salvar triagem ✓'}</button>
  </form></Page>;
}
function About() {
  const [error, setError] = useState('');
  return <Page title="Sobre o projeto"><section className="card stack"><h2>O que é o Triar?</h2><p>O Triar é um aplicativo de triagens em saúde desenvolvido para fins acadêmicos. Ele permite aplicar questionários e obter uma classificação educativa do resultado.</p><h2>Como as triagens funcionam?</h2><p>As respostas somam pesos à pontuação final, comparada com as faixas de resultado configuradas. Os resultados não constituem diagnóstico.</p><h2>Crie suas próprias triagens</h2><p>Defina perguntas, pesos e faixas de resultado. Suas triagens ficam salvas na home e podem ser aplicadas em quantas pessoas você quiser.</p><h2 className="danger-text">Aviso importante</h2><p>Protótipo acadêmico ainda não homologado clinicamente. O resultado é educativo e não deve orientar diagnóstico, tratamento ou urgência.</p></section>{getMode() === 'individual' && <section className="card stack local-backup"><h2>Dados individuais</h2><p>O SQLite fica salvo neste navegador. Limpar os dados do site remove o banco. Faça uma cópia antes de trocar de aparelho.</p><button onClick={async () => { try { const { exportSqlite } = await import('./local-db'); await exportSqlite(); } catch (e) { setError(message(e)); } }}>Baixar cópia do SQLite</button><ErrorNotice error={error} /></section>}</Page>;
}
