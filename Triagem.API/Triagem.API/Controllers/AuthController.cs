using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;
using Triagem.API.Data;
using Triagem.API.Dtos;
using Triagem.API.Models;
using Triagem.API.Services;

namespace Triagem.API.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
[EnableRateLimiting("auth")]
[RequestSizeLimit(32 * 1024)]
public class AuthController(TriagemDbContext db, TokenService tokens, ILogger<AuthController> logger) : ControllerBase
{
    private const int SenhaMinima = 8;

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nome) ||
            string.IsNullOrWhiteSpace(req.Email) ||
            string.IsNullOrWhiteSpace(req.Senha))
            return BadRequest("Preencha nome, email e senha.");

        if (req.Senha.Length < SenhaMinima)
            return BadRequest($"A senha deve ter pelo menos {SenhaMinima} caracteres.");
        if (req.Senha.Length > 256)
            return BadRequest("A senha deve ter no máximo 256 caracteres.");
        if (req.Nome.Trim().Length > 120)
            return BadRequest("O nome deve ter no máximo 120 caracteres.");
        if (req.Email.Trim().Length > 180 || !MailAddress.TryCreate(req.Email.Trim(), out _))
            return BadRequest("Informe um email válido.");

        var email = req.Email.Trim().ToLowerInvariant();
        // Executa o mesmo trabalho de hash para emails novos e já existentes, reduzindo
        // a diferença de tempo observável sem alterar o fluxo de cadastro imediato.
        var senhaHash = PasswordHasher.Hash(req.Senha);

        if (await db.Usuarios.AnyAsync(u => u.Email == email, ct))
            return Conflict("Não foi possível criar a conta com os dados informados.");

        var usuario = new Usuario
        {
            Nome = req.Nome.Trim(),
            Email = email,
            SenhaHash = senhaHash
        };

        db.Usuarios.Add(usuario);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Duas solicitações concorrentes podem passar pelo AnyAsync; o índice único
            // continua sendo a autoridade e a segunda recebe o mesmo erro funcional.
            db.Entry(usuario).State = EntityState.Detached;
            if (await db.Usuarios.AsNoTracking().AnyAsync(u => u.Email == email, ct))
                return Conflict("Não foi possível criar a conta com os dados informados.");
            throw;
        }

        logger.LogInformation("Novo usuário cadastrado: {Email}", email);
        return Ok(Autenticar(usuario));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Senha))
            return BadRequest("Preencha email e senha.");

        var email = req.Email.Trim().ToLowerInvariant();
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Email == email, ct);

        if (usuario is null || !PasswordHasher.Verify(req.Senha, usuario.SenhaHash))
            return Unauthorized("Email ou senha inválidos.");

        return Ok(Autenticar(usuario));
    }

    private AuthResponse Autenticar(Usuario usuario)
    {
        var (token, expiraEm) = tokens.Gerar(usuario);
        return new AuthResponse(usuario.Id, usuario.Nome, usuario.Email, token, expiraEm);
    }
}
