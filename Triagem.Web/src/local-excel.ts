import writeXlsxFile from 'write-excel-file/universal';
import type { Historico } from './models';

export async function exportExcel(items: Historico[]) {
  const columns = ['Triagem', 'Nome', 'Idade', 'Sexo', 'Pontuação', 'Máximo', 'Resultado', 'Data'];
  const rows = items.map(item => [item.tituloTriagem, item.nome, String(item.idade), item.sexo, String(item.pontuacao), String(item.pontuacaoMaxima), item.resultado, new Date(item.data).toLocaleString('pt-BR')].map(value => ({ type: String, value })));
  const blob = await writeXlsxFile([columns.map(value => ({ type: String, value, fontWeight: 'bold' as const })), ...rows]).toBlob();
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a'); link.href = url; link.download = 'Triagens.xlsx'; link.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
