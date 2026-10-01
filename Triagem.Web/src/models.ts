export interface Auth { id: number; nome: string; email: string; token: string; expiraEm: string }
export interface Resumo {
  id: number; titulo: string; publicoAlvo: string; descricao: string; icone: string;
  padrao: boolean; minhaAutoria: boolean; visivelNaHome: boolean; totalPerguntas: number;
}
export interface Pergunta { id: number; texto: string; peso: number; ordem: number; categoria?: string; opcoes?: string[] }
export interface Faixa { titulo: string; recomendacao: string; pontuacaoMin: number; pontuacaoMax: number; cor: string }
export interface ModeloInput { titulo: string; publicoAlvo: string; descricao: string; icone: string; perguntas: { texto: string; peso: number }[]; faixas: Faixa[] }
export interface Detalhe extends ModeloInput { id: number; padrao: boolean; criadorUsuarioId: number | null; perguntas: Pergunta[] }
export interface Resultado {
  id: number; triagemModeloId: number; tituloTriagem: string; nomePaciente: string; idade: number;
  sexo: string; pontuacao: number; pontuacaoMaxima: number; classificacao: string; recomendacao: string; cor: string; data: string; escolaridade?: string; doencasPrevias?: string;
}
export interface Historico { id: number; triagemModeloId: number; tituloTriagem: string; nome: string; idade: number; sexo: string; pontuacao: number; pontuacaoMaxima: number; resultado: string; cor: string; data: string }
