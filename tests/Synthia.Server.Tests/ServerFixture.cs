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
            .WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:Synthia", cs));

        // Força a inicialização (e as migrações) antes do primeiro teste.
        Factory.CreateClient().Dispose();
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
