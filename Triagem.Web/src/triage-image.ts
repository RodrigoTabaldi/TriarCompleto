// Mesmo mapeamento de TriagemImagem.ObterArquivoPadrao no MAUI atualizado.
export function triageImage(title: string) {
  const text = title.toLowerCase();
  if (/linguagem|cogni|saúde mental|saude mental/.test(text)) return '/assets/triagem_linguagem_cognicao.svg';
  if (/infantil|criança|crianca/.test(text)) return '/assets/triagem_fono_infantil.svg';
  if (/orofacial|motricidade|mastiga|degluti|saúde da mulher|saude da mulher/.test(text)) return '/assets/triagem_motricidade_orofacial.svg';
  if (/idoso/.test(text) && /audi|ouvi/.test(text)) return '/assets/triagem_auditiva_idoso.svg';
  if (/voz|vocal|laringe|respiratória|respiratoria/.test(text)) return '/assets/triagem_voz.svg';
  if (/audi|ouvi|clínica geral|clinica geral/.test(text)) return '/assets/triagem_auditiva.svg';
  return '/assets/triagem_fonoaudiologia.svg';
}
