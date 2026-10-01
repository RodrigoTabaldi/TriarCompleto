import { useState } from 'react';
import type { Mode } from './api';

export function ModeChoice({ onChoose, busy, error }: { onChoose: (mode: Mode) => void; busy: boolean; error: string }) {
  const [selected, setSelected] = useState<Mode | null>(null);
  return <main className="mode-page"><section className="mode-panel"><img className="logo" src="/assets/logo.png" alt="Triar" /><h1>Como você quer fazer a triagem?</h1><p className="muted">Escolha o tipo de atendimento para continuar</p>
    <div className="mode-options" role="group" aria-label="Tipo de atendimento">
      <button className={`mode-card ${selected === 'individual' ? 'chosen' : ''}`} aria-pressed={selected === 'individual'} disabled={busy} onClick={() => setSelected('individual')}><img src="/assets/icone_individual.png" alt="" /><strong>Triagem Individual</strong><span>Resultado exibido na hora, ideal para um atendimento rápido.</span><span className="mode-badge"><img src="/assets/icone_privado.png" alt="" />Não salva dados na nuvem</span><span className="mode-select">Selecionar →</span></button>
      <button className={`mode-card ${selected === 'grupo' ? 'chosen' : ''}`} aria-pressed={selected === 'grupo'} disabled={busy} onClick={() => setSelected('grupo')}><img src="/assets/icone_grupo.png" alt="" /><strong>Triagem em Grupo</strong><span>Faça a triagem de várias pessoas e acompanhe o histórico de cada uma.</span><span className="mode-badge"><img src="/assets/icone_salvar.png" alt="" />Salva dados</span><span className="mode-select">Selecionar →</span></button>
    </div>{error && <div className="error" role="alert">{error}</div>}<button className="continue" disabled={!selected || busy} onClick={() => selected && onChoose(selected)}>{busy ? 'Aguarde…' : 'Continuar'}</button>
  </section></main>;
}
