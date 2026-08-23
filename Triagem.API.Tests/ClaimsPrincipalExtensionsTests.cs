using System.Security.Claims;
using Triagem.API.Services;

namespace Triagem.API.Tests;

public class ClaimsPrincipalExtensionsTests
{
    private static ClaimsPrincipal ComClaim(string? valor)
    {
        var claims = new List<Claim>();
        if (valor is not null) claims.Add(new Claim(TokenService.UserIdClaim, valor));
        return new ClaimsPrincipal(new ClaimsIdentity(claims));
    }

    [Fact]
    public void GetUserId_ComClaimValido_RetornaId()
    {
        var user = ComClaim("7");

        Assert.Equal(7, user.GetUserId());
    }

    [Fact]
    public void GetUserId_SemClaim_Lanca()
    {
        var user = ComClaim(null);

        Assert.Throws<UnauthorizedAccessException>(() => user.GetUserId());
    }

    [Fact]
    public void GetUserId_ComClaimNaoNumerico_Lanca()
    {
        var user = ComClaim("nao-e-um-numero");

        Assert.Throws<UnauthorizedAccessException>(() => user.GetUserId());
    }
}
