using System.IdentityModel.Tokens.Jwt;
using Triagem.API.Models;
using Triagem.API.Services;

namespace Triagem.API.Tests;

public class TokenServiceTests
{
    private static TokenService NovoTokenService(int expiresHours = 12) =>
        new(new JwtOptions
        {
            Key = "chave-de-teste-com-mais-de-32-caracteres-para-jwt",
            Issuer = "TriarAPI.Tests",
            Audience = "TriarApp.Tests",
            ExpiresHours = expiresHours
        });

    [Fact]
    public void Gerar_IncluiClaimUidComIdDoUsuario()
    {
        var tokens = NovoTokenService();
        var usuario = new Usuario { Id = 42, Nome = "Ana", Email = "ana@teste.com" };

        var (token, _) = tokens.Gerar(usuario);
        var lido = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("42", lido.Claims.First(c => c.Type == TokenService.UserIdClaim).Value);
    }

    [Fact]
    public void Gerar_RespeitaExpiresHoursConfigurado()
    {
        var tokens = NovoTokenService(expiresHours: 2);
        var usuario = new Usuario { Id = 1, Nome = "Ana", Email = "ana@teste.com" };

        var (_, expiraEm) = tokens.Gerar(usuario);

        Assert.True(expiraEm > DateTime.UtcNow.AddHours(1.9));
        Assert.True(expiraEm < DateTime.UtcNow.AddHours(2.1));
    }

    [Fact]
    public void Gerar_DoisTokensParaOMesmoUsuario_TemJtiDiferente()
    {
        var tokens = NovoTokenService();
        var usuario = new Usuario { Id = 1, Nome = "Ana", Email = "ana@teste.com" };

        var (token1, _) = tokens.Gerar(usuario);
        var (token2, _) = tokens.Gerar(usuario);

        var handler = new JwtSecurityTokenHandler();
        var jti1 = handler.ReadJwtToken(token1).Claims.First(c => c.Type == "jti").Value;
        var jti2 = handler.ReadJwtToken(token2).Claims.First(c => c.Type == "jti").Value;

        Assert.NotEqual(jti1, jti2);
    }
}
