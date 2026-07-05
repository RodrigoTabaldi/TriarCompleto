using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Triagem.API.Dtos;
using Triagem.API.Services;

namespace Triagem.API.Controllers;

[ApiController]
[Route("api/triagens")]
[EnableRateLimiting("api")]
public class TriagensController(TriagemService service) : ControllerBase
{
    /// <summary>Lista as triagens disponíveis para o usuário (padrão + criadas por ele), com flag de visibilidade na home.</summary>
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] int usuarioId) =>
        Ok(await service.ListarParaUsuarioAsync(usuarioId));

    /// <summary>Detalhe de uma triagem: perguntas (com pesos) e faixas de resultado.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Obter(int id)
    {
        var detalhe = await service.ObterDetalheAsync(id);
        return detalhe is null ? NotFound("Triagem não encontrada.") : Ok(detalhe);
    }

    /// <summary>Cria uma triagem personalizada (perguntas sim/não com pesos + faixas de resultado).</summary>
    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarTriagemRequest req)
    {
        var (detalhe, erro) = await service.CriarAsync(req);
        return erro is null ? Ok(detalhe) : BadRequest(erro);
    }

    /// <summary>Edita uma triagem criada pelo usuário.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Atualizar(int id, [FromBody] CriarTriagemRequest req)
    {
        var (ok, erro) = await service.AtualizarAsync(id, req);
        return ok ? Ok() : BadRequest(erro);
    }

    /// <summary>Remove (desativa) uma triagem criada pelo usuário.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Excluir(int id, [FromQuery] int usuarioId)
    {
        var (ok, erro) = await service.DesativarAsync(id, usuarioId);
        return ok ? Ok() : BadRequest(erro);
    }

    /// <summary>Responde uma triagem e retorna o resultado calculado.</summary>
    [HttpPost("{id:int}/responder")]
    public async Task<IActionResult> Responder(int id, [FromBody] ResponderTriagemRequest req)
    {
        var (resultado, erro) = await service.ResponderAsync(id, req);
        return erro is null ? Ok(resultado) : BadRequest(erro);
    }

    /// <summary>Histórico de aplicações de uma triagem pelo usuário.</summary>
    [HttpGet("{id:int}/historico")]
    public async Task<IActionResult> Historico(int id, [FromQuery] int usuarioId) =>
        Ok(await service.HistoricoAsync(usuarioId, id));
}

/// <summary>Rotas de compatibilidade com versões antigas do app + histórico geral.</summary>
[ApiController]
[Route("api/triagem")]
[EnableRateLimiting("api")]
public class TriagemLegacyController(TriagemService service) : ControllerBase
{
    [HttpGet("usuario/{usuarioId:int}")]
    public async Task<IActionResult> HistoricoDoUsuario(int usuarioId, [FromQuery] int? triagemModeloId) =>
        Ok(await service.HistoricoAsync(usuarioId, triagemModeloId));
}

[ApiController]
[Route("api/usuarios")]
[EnableRateLimiting("api")]
public class UsuariosController(TriagemService service) : ControllerBase
{
    /// <summary>Define quais triagens aparecem na home do usuário e em que ordem.</summary>
    [HttpPut("{usuarioId:int}/home")]
    public async Task<IActionResult> ConfigurarHome(int usuarioId, [FromBody] ConfigurarHomeRequest req)
    {
        await service.ConfigurarHomeAsync(usuarioId, req);
        return Ok();
    }
}
