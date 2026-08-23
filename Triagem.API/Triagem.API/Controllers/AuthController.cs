using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Triagem.API.Data;
using Triagem.API.Dtos;
using Triagem.API.Models;
using Triagem.API.Services;

namespace Triagem.API.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
[EnableRateLimiting("auth")]
public class AuthController(TriagemDbContext db, TokenService tokens, ILogger<AuthController> logger) : ControllerBase
{
    private const int SenhaMinima = 8;

    // Hash de custo idêntico ao de uma senha real, verificado quando o email não
    // existe, para que Login gaste sempre o mesmo tempo de PBKDF2 — sem isto, o
    // curto-circuito de "usuario is null" torna a resposta mensuravelmente mais rápida
    // para emails inexistentes, um oráculo de enumeração de contas por timing.
    private static readonly string HashParaEmailInexistente = PasswordHasher.Hash(Guid.NewGuid().ToString("N"));

    [HttpPost("register")]
    [DistributedAuthRateLimit]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Nome) ||
            string.IsNullOrWhiteSpace(req.Email) ||
            string.IsNullOrWhiteSpace(req.Senha))
            return BadRequest("Preencha nome, email e senha.");

        if (req.Senha.Length < SenhaMinima)
            return BadRequest($"A senha deve ter pelo menos {SenhaMinima} caracteres.");

        var email = req.Email.Trim().ToLowerInvariant();

        if (await db.Usuarios.AnyAsync(u => u.Email == email))
            return Conflict("Já existe uma conta com este email.");

        var usuario = new Usuario
        {
            Nome = req.Nome.Trim(),
            Email = email,
            SenhaHash = PasswordHasher.Hash(req.Senha)
        };

        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();

        logger.LogInformation("Novo usuário cadastrado: {UsuarioId}", usuario.Id);
        return Ok(Autenticar(usuario));
    }

    [HttpPost("login")]
    [DistributedAuthRateLimit]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Senha))
            return BadRequest("Preencha email e senha.");

        var email = req.Email.Trim().ToLowerInvariant();
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Email == email);

        // Sempre executa o PBKDF2 (contra o hash real ou, se o email não existe,
        // contra um hash fictício de mesmo custo) para não vazar por timing quais
        // emails têm conta.
        var senhaValida = PasswordHasher.Verify(req.Senha, usuario?.SenhaHash ?? HashParaEmailInexistente);

        if (usuario is null || !senhaValida)
            return Unauthorized("Email ou senha inválidos.");

        return Ok(Autenticar(usuario));
    }

    private AuthResponse Autenticar(Usuario usuario)
    {
        var (token, expiraEm) = tokens.Gerar(usuario);
        return new AuthResponse(usuario.Id, usuario.Nome, usuario.Email, token, expiraEm);
    }
}
