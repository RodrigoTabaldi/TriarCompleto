using System.Security.Claims;

namespace Triagem.API.Services;

/// <summary>
/// Acesso à identidade autenticada. A API confia SEMPRE no token, nunca em um
/// "usuarioId" vindo da query/body do cliente — isso fecha a falha de IDOR.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>Id do usuário autenticado, extraído do JWT. Lança se não houver identidade válida.</summary>
    public static int GetUserId(this ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(TokenService.UserIdClaim);
        if (int.TryParse(raw, out var id)) return id;
        throw new UnauthorizedAccessException("Token sem identidade de usuário válida.");
    }
}
