using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace Synthia.Server.Tests;

/// <summary>
/// Sobe a API contra um banco Postgres descartável. Configure SYNTHIA_TEST_PG com uma
/// connection string de um usuário que possa criar bancos.
/// </summary>
public sealed class ServerFixture : IAsyncLifetime
{
    private readonly string _adminConnectionString =
        Environment.GetEnvironmentVariable("SYNTHIA_TEST_PG")
        ?? "Host=localhost;Username=synthia;Password=synthia;Database=postgres";

    private readonly string _database = $"synthia_test_{Guid.NewGuid():N}";

    /// <summary>Chave de API do back-office configurada para os testes (integrações/automação).</summary>
    public const string ApiKey = "chave-de-teste-da-api";

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public NpgsqlDataSource Db { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await using (var admin = new NpgsqlConnection(_adminConnectionString))
        {
            await admin.OpenAsync();
            await using var cmd = new NpgsqlCommand($"CREATE DATABASE {_database}", admin);
            await cmd.ExecuteNonQueryAsync();
        }

        var cs = new NpgsqlConnectionStringBuilder(_adminConnectionString) { Database = _database }.ConnectionString;
        Db = NpgsqlDataSource.Create(cs);
        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b
                .UseSetting("ConnectionStrings:Synthia", cs)
                .UseSetting("Backoffice:ApiKeys:0", ApiKey));

        // Força a inicialização (e as migrações) antes do primeiro teste.
        Factory.CreateClient().Dispose();
    }

    /// <summary>Cliente HTTP autenticado na API do back-office pela chave de API.</summary>
    public HttpClient ApiClient()
    {
        var http = Factory.CreateClient();
        http.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);
        return http;
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await Db.DisposeAsync();
        NpgsqlConnection.ClearAllPools();

        await using var admin = new NpgsqlConnection(_adminConnectionString);
        await admin.OpenAsync();
        await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS {_database} WITH (FORCE)", admin);
        await cmd.ExecuteNonQueryAsync();
    }
}
