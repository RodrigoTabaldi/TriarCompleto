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
[EnableRateLimiting("auth")]
public class AuthController(TriagemDbContext db, ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Nome) ||
            string.IsNullOrWhiteSpace(req.Email) ||
            string.IsNullOrWhiteSpace(req.Senha))
            return BadRequest("Preencha nome, email e senha.");

        if (req.Senha.Length < 4)
            return BadRequest("A senha deve ter pelo menos 4 caracteres.");

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

        logger.LogInformation("Novo usuário cadastrado: {Email}", email);
        return Ok(new UsuarioResponse(usuario.Id, usuario.Nome, usuario.Email));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Senha))
            return BadRequest("Preencha email e senha.");

        var email = req.Email.Trim().ToLowerInvariant();
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Email == email);

        if (usuario is null || !PasswordHasher.Verify(req.Senha, usuario.SenhaHash))
            return Unauthorized("Email ou senha inválidos.");

        return Ok(new UsuarioResponse(usuario.Id, usuario.Nome, usuario.Email));
    }
}
