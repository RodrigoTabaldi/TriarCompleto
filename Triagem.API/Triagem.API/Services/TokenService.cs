using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Triagem.API.Models;

namespace Triagem.API.Services;

/// <summary>
/// Opções de emissão do JWT. Preenchidas a partir da seção "Jwt" da configuração.
/// A chave (Key) NUNCA deve ser versionada: vem de variável de ambiente/secret.
/// </summary>
public class JwtOptions
{
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "TriarAPI";
    public string Audience { get; set; } = "TriarApp";
    public int ExpiresHours { get; set; } = 12;
}

/// <summary>
/// Emite tokens JWT assinados (HMAC-SHA256) com a identidade do usuário.
/// A autorização da API passa a derivar o usuário do token — nunca de um id enviado pelo cliente.
/// </summary>
public class TokenService(JwtOptions options)
{
    public const string UserIdClaim = "uid";

    public (string Token, DateTime ExpiraEm) Gerar(Usuario usuario)
    {
        var expiraEm = DateTime.UtcNow.AddHours(options.ExpiresHours);

        var claims = new[]
        {
            new Claim(UserIdClaim, usuario.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, usuario.Email),
            new Claim(JwtRegisteredClaimNames.Name, usuario.Nome),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key));
        var credenciais = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            expires: expiraEm,
            signingCredentials: credenciais);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiraEm);
    }
}
