using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Triagem.API.Dtos;
using Triagem.API.Services;

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
        var detalhe = await service.ObterDetalheAsync(User.GetUserId(), id);
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

    /// <summary>Histórico de aplicações de uma triagem pelo usuário autenticado (paginado).</summary>
    [HttpGet("{id:int}/historico")]
    public async Task<IActionResult> Historico(int id, [FromQuery] int pagina = 1, [FromQuery] int tamanhoPagina = 100) =>
        Ok(await service.HistoricoAsync(User.GetUserId(), id, pagina, tamanhoPagina));
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
    public async Task<IActionResult> HistoricoDoUsuario(
        int usuarioId, [FromQuery] int? triagemModeloId, [FromQuery] int pagina = 1, [FromQuery] int tamanhoPagina = 100) =>
        Ok(await service.HistoricoAsync(User.GetUserId(), triagemModeloId, pagina, tamanhoPagina));
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
