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
    public async Task<IActionResult> Listar(CancellationToken ct) =>
        Ok(await service.ListarParaUsuarioAsync(User.GetUserId(), ct));

    /// <summary>Detalhe de uma triagem: perguntas (com pesos) e faixas de resultado.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Obter(int id, CancellationToken ct)
    {
        var detalhe = await service.ObterDetalheAsync(User.GetUserId(), id, ct);
        return detalhe is null ? NotFound("Triagem não encontrada.") : Ok(detalhe);
    }

    /// <summary>Cria uma triagem personalizada (perguntas sim/não com pesos + faixas de resultado).</summary>
    [HttpPost]
    [RequestSizeLimit(4 * 1024 * 1024)]
    public async Task<IActionResult> Criar([FromBody] CriarTriagemRequest req, CancellationToken ct)
    {
        var (detalhe, erro) = await service.CriarAsync(User.GetUserId(), req, ct);
        return erro is null ? Ok(detalhe) : BadRequest(erro);
    }

    /// <summary>Edita uma triagem criada pelo usuário autenticado.</summary>
    [HttpPut("{id:int}")]
    [RequestSizeLimit(4 * 1024 * 1024)]
    public async Task<IActionResult> Atualizar(int id, [FromBody] CriarTriagemRequest req, CancellationToken ct)
    {
        var (ok, erro) = await service.AtualizarAsync(User.GetUserId(), id, req, ct);
        return ok ? Ok() : BadRequest(erro);
    }

    /// <summary>Remove (desativa) uma triagem criada pelo usuário autenticado.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Excluir(int id, CancellationToken ct)
    {
        var (ok, erro) = await service.DesativarAsync(User.GetUserId(), id, ct);
        return ok ? Ok() : BadRequest(erro);
    }

    /// <summary>Responde uma triagem e retorna o resultado calculado.</summary>
    [HttpPost("{id:int}/responder")]
    [RequestSizeLimit(64 * 1024)]
    public async Task<IActionResult> Responder(int id, [FromBody] ResponderTriagemRequest req, CancellationToken ct)
    {
        var (resultado, erro) = await service.ResponderAsync(User.GetUserId(), id, req, ct);
        return erro is null ? Ok(resultado) : BadRequest(erro);
    }

    /// <summary>Histórico de aplicações de uma triagem pelo usuário autenticado (paginado).</summary>
    [HttpGet("{id:int}/historico")]
    public async Task<IActionResult> Historico(int id, [FromQuery] int pagina = 1, [FromQuery] int tamanhoPagina = 100, CancellationToken ct = default) =>
        Ok(await service.HistoricoAsync(User.GetUserId(), id, pagina, tamanhoPagina, ct));
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
        int usuarioId, [FromQuery] int? triagemModeloId, [FromQuery] int pagina = 1, [FromQuery] int tamanhoPagina = 100,
        CancellationToken ct = default) =>
        Ok(await service.HistoricoAsync(User.GetUserId(), triagemModeloId, pagina, tamanhoPagina, ct));
}

[ApiController]
[Authorize]
[Route("api/usuarios")]
[EnableRateLimiting("api")]
public class UsuariosController(TriagemService service) : ControllerBase
{
    /// <summary>Define quais triagens aparecem na home do usuário autenticado (sempre o do token — nunca um id da rota).</summary>
    [HttpPut("home")]
    [RequestSizeLimit(64 * 1024)]
    public async Task<IActionResult> ConfigurarHome([FromBody] ConfigurarHomeRequest req, CancellationToken ct)
    {
        var erro = await service.ConfigurarHomeAsync(User.GetUserId(), req, ct);
        return erro is null ? Ok() : BadRequest(erro);
    }
}
