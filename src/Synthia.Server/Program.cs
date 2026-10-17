using Npgsql;
using Synthia.Contracts.Catalog;
using Synthia.Contracts.Sync;
using Synthia.Server.Catalog;
using Synthia.Server.Data;
using Synthia.Server.Sync;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(
    builder.Configuration.GetConnectionString("Synthia")
    ?? throw new InvalidOperationException("ConnectionStrings:Synthia não configurada.")));
builder.Services.AddSingleton<Migrator>();
builder.Services.AddSingleton<Projector>();
builder.Services.AddSingleton<EventStore>();
builder.Services.AddSingleton<CatalogService>();

var app = builder.Build();

await app.Services.GetRequiredService<Migrator>().MigrateAsync();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Caixa → servidor
app.MapPost(SyncRoutes.Events, async (PushEventsRequest request, EventStore store, CancellationToken ct) =>
{
    if (request.Events.Count > 1000)
        return Results.BadRequest("Lote acima de 1000 eventos.");

    var accepted = await store.AppendAsync(request.Events, ct);
    return Results.Ok(new PushEventsResponse(accepted));
});

// Servidor → caixas
app.MapGet(SyncRoutes.Catalog, (long since, CatalogService catalog, CancellationToken ct) =>
    catalog.GetDeltaAsync(since, ct));

app.MapGet(SyncRoutes.CatalogSnapshot, (CatalogService catalog, CancellationToken ct) =>
    catalog.GetSnapshotAsync(ct));

// Back-office (mínimo, até existir a interface web)
var backoffice = app.MapGroup("/backoffice");

backoffice.MapGet("/products", (CatalogService catalog, CancellationToken ct) => catalog.ListProductsAsync(ct));

backoffice.MapPut("/products/{id:guid}", async (Guid id, ProductData product, CatalogService catalog, CancellationToken ct) =>
{
    if (id != product.Id) return Results.BadRequest("Id da rota difere do corpo.");
    var version = await catalog.UpsertProductAsync(product, ct);
    return Results.Ok(new { version });
});

backoffice.MapPost("/scheduled-prices", async (ScheduledPriceData price, CatalogService catalog, CancellationToken ct) =>
{
    var version = await catalog.SchedulePriceAsync(price, ct);
    return Results.Ok(new { version });
});

backoffice.MapPost("/projections/rebuild", async (EventStore store, CancellationToken ct) =>
{
    await store.RebuildProjectionsAsync(ct);
    return Results.NoContent();
});

app.Run();

public partial class Program;
