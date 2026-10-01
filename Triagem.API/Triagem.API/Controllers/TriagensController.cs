using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Triagem.API.Dtos;
using Triagem.API.Services;
using ClosedXML.Excel;

namespace Triagem.API.Controllers;

[ApiController]
[Authorize]
[Route("api/triagens")]
[EnableRateLimiting("api")]
public class TriagensController(TriagemService service) : ControllerBase
{
    /// <summary>Lista as triagens disponíveis para o usuário autenticado (padrão + criadas por ele).</summary>
    [HttpGet]
    public async Task<IActionResult> Listar() =>
        Ok(await service.ListarParaUsuarioAsync(User.GetUserId()));

    /// <summary>Detalhe de uma triagem: perguntas (com pesos) e faixas de resultado.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Obter(int id)
    {
        var detalhe = await service.ObterDetalheAsync(id);
        if (detalhe?.CriadorUsuarioId is { } criador && criador != User.GetUserId())
            return NotFound("Triagem não encontrada.");
        return detalhe is null ? NotFound("Triagem não encontrada.") : Ok(detalhe);
    }

    /// <summary>Cria uma triagem personalizada (perguntas sim/não com pesos + faixas de resultado).</summary>
    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarTriagemRequest req)
    {
        var (detalhe, erro) = await service.CriarAsync(User.GetUserId(), req);
        return erro is null ? Ok(detalhe) : BadRequest(erro);
    }

    /// <summary>Edita uma triagem criada pelo usuário autenticado.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Atualizar(int id, [FromBody] CriarTriagemRequest req)
    {
        var (ok, erro) = await service.AtualizarAsync(User.GetUserId(), id, req);
        return ok ? Ok() : BadRequest(erro);
    }

    /// <summary>Remove (desativa) uma triagem criada pelo usuário autenticado.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Excluir(int id)
    {
        var (ok, erro) = await service.DesativarAsync(User.GetUserId(), id);
        return ok ? Ok() : BadRequest(erro);
    }

    /// <summary>Responde uma triagem e retorna o resultado calculado.</summary>
    [HttpPost("{id:int}/responder")]
    public async Task<IActionResult> Responder(int id, [FromBody] ResponderTriagemRequest req)
    {
        var (resultado, erro) = await service.ResponderAsync(User.GetUserId(), id, req);
        return erro is null ? Ok(resultado) : BadRequest(erro);
    }

    /// <summary>Histórico de aplicações de uma triagem pelo usuário autenticado.</summary>
    [HttpGet("{id:int}/historico")]
    public async Task<IActionResult> Historico(int id) =>
        Ok(await service.HistoricoAsync(User.GetUserId(), id));

    [HttpGet("{id:int}/historico/excel")]
    public async Task<IActionResult> ExportarExcel(int id)
    {
        var itens = await service.HistoricoAsync(User.GetUserId(), id == 0 ? null : id);
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Triagens");
        string[] headers = ["Triagem", "Nome", "Idade", "Sexo", "Pontuação", "Máximo", "Resultado", "Data (UTC)"];
        for (var c = 0; c < headers.Length; c++) sheet.Cell(1, c + 1).Value = headers[c];
        for (var i = 0; i < itens.Count; i++)
        {
            var item = itens[i];
            var row = i + 2;
            sheet.Cell(row, 1).Value = item.TituloTriagem;
            sheet.Cell(row, 2).Value = item.Nome;
            sheet.Cell(row, 3).Value = item.Idade;
            sheet.Cell(row, 4).Value = item.Sexo;
            sheet.Cell(row, 5).Value = item.Pontuacao;
            sheet.Cell(row, 6).Value = item.PontuacaoMaxima;
            sheet.Cell(row, 7).Value = item.Resultado;
            sheet.Cell(row, 8).Value = item.Data.ToString("yyyy-MM-dd HH:mm:ss");
        }
        sheet.Range(1, 1, 1, headers.Length).Style.Font.Bold = true;
        sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Triagens.xlsx");
    }
}

/// <summary>Rota de compatibilidade com versões antigas do app + histórico geral (sempre do usuário autenticado).</summary>
[ApiController]
[Authorize]
[Route("api/triagem")]
[EnableRateLimiting("api")]
public class TriagemLegacyController(TriagemService service) : ControllerBase
{
    /// <summary>O id do usuário é ignorado: o histórico é sempre o do token autenticado (evita IDOR).</summary>
    [HttpGet("usuario/{usuarioId:int}")]
    public async Task<IActionResult> HistoricoDoUsuario(int usuarioId, [FromQuery] int? triagemModeloId) =>
        Ok(await service.HistoricoAsync(User.GetUserId(), triagemModeloId));
}

[ApiController]
[Authorize]
[Route("api/usuarios")]
[EnableRateLimiting("api")]
public class UsuariosController(TriagemService service) : ControllerBase
{
    /// <summary>Define quais triagens aparecem na home do usuário autenticado.</summary>
    [HttpPut("{usuarioId:int}/home")]
    public async Task<IActionResult> ConfigurarHome(int usuarioId, [FromBody] ConfigurarHomeRequest req)
    {
        await service.ConfigurarHomeAsync(User.GetUserId(), req);
        return Ok();
    }
}
