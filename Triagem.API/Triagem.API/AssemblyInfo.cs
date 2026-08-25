using System.Runtime.CompilerServices;

// Permite que Triagem.API.Tests exercite tipos internos (ex.: WebApplicationFactory<Program>
// para testes de integração, e detalhes internos como AuthController.ResolverHashParaComparacao).
[assembly: InternalsVisibleTo("Triagem.API.Tests")]
