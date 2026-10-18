using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.FileProviders;
using Npgsql;
using Synthia.Contracts.Catalog;
using Synthia.Contracts.Sync;
using Synthia.Server.Auth;
using Synthia.Server.Backoffice;
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
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<BackofficeEvents>();
builder.Services.AddSingleton<StockService>();
builder.Services.AddSingleton<InventoryService>();
builder.Services.AddSingleton<NfeImport>();
builder.Services.AddSingleton<NfceQueueService>();
builder.Services.AddSingleton<BackofficeUsers>();

// Retaguarda (Blazor Server) com login por cookie.
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddScheme<AuthenticationSchemeOptions, TerminalKeyHandler>(ApiAuth.TerminalScheme, null)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyHandler>(ApiAuth.ApiKeyScheme, null)
    .AddCookie(options =>
    {
        options.LoginPath = "/retaguarda/login";
        options.AccessDeniedPath = "/retaguarda/sem-permissao";
        options.Cookie.Name = "synthia.retaguarda";
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
        // API não redireciona para a tela de login: responde 401/403.
        static bool IsApi(HttpRequest r) => r.Path.StartsWithSegments("/backoffice") || r.Path.StartsWithSegments("/sync");
        options.Events.OnRedirectToLogin = context =>
        {
            if (IsApi(context.Request)) context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            else context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            if (IsApi(context.Request)) context.Response.StatusCode = StatusCodes.Status403Forbidden;
            else context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
        options.Events.OnValidatePrincipal = async context =>
        {
            // Usuário desativado (ou apagado) perde o acesso na próxima requisição, sem esperar o cookie expirar.
            var id = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var users = context.HttpContext.RequestServices.GetRequiredService<BackofficeUsers>();
            if (!Guid.TryParse(id, out var userId) || !await users.IsActiveAsync(userId))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(ApiAuth.TerminalPolicy, p => p.AddAuthenticationSchemes(ApiAuth.TerminalScheme).RequireClaim(ApiAuth.TerminalClaim));
    options.AddPolicy(ApiAuth.BackofficeApiPolicy, p => p
        .AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme, ApiAuth.ApiKeyScheme)
        .RequireRole(BackofficeRoles.Admin, BackofficeRoles.Manager, ApiAuth.ApiRole));
});

builder.Services.AddOpenApi(options => options.AddDocumentTransformer((doc, _, _) =>
{
    doc.Info.Title = "PDV Synthia · Servidor local";
    doc.Info.Description = "Sync caixa ↔ servidor e back-office. Documentação interativa em /docs.";
    return Task.CompletedTask;
}));

var app = builder.Build();

await app.Services.GetRequiredService<Migrator>().MigrateAsync();
await app.Services.GetRequiredService<BackofficeUsers>().EnsureAdminAsync();

// Documentação interativa (docs/ do repositório, copiada para a saída do build) + OpenAPI.
var docsPath = Path.Combine(AppContext.BaseDirectory, "docs");
if (Directory.Exists(docsPath))
{
    var docs = new PhysicalFileProvider(docsPath);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = docs, RequestPath = "/docs" });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = docs, RequestPath = "/docs" });
    app.MapGet("/", () => Results.Redirect("/docs/")).ExcludeFromDescription();
}
app.MapOpenApi();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<Synthia.Server.Retaguarda.App>().AddInteractiveServerRenderMode();

app.MapPost("/retaguarda/logout", async (HttpContext http, IAntiforgery antiforgery) =>
{
    await antiforgery.ValidateRequestAsync(http);
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/retaguarda/login");
}).ExcludeFromDescription();

// Modelos de planilha (UTF-8 com BOM para o Excel abrir com acentos).
static IResult Csv(string content, string name) =>
    Results.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(content)).ToArray(), "text/csv", name);
app.MapGet("/retaguarda/modelos/produtos.csv", () => Csv(ProductImport.Template, "modelo-produtos.csv")).ExcludeFromDescription();
app.MapGet("/retaguarda/modelos/entrada.csv", () => Csv(ReceiptImport.Template, "modelo-entrada.csv")).ExcludeFromDescription();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithTags("Sistema").WithName("Health")
    .WithSummary("Verifica se o servidor está no ar");

// Caixa → servidor
var sync = app.MapGroup("").WithTags("Sync").RequireAuthorization(ApiAuth.TerminalPolicy);

sync.MapPost(SyncRoutes.Events, async (PushEventsRequest request, EventStore store, ClaimsPrincipal caller, CancellationToken ct) =>
{
    if (request.Events.Count > 1000)
        return Results.BadRequest("Lote acima de 1000 eventos.");

    // Um caixa só envia os próprios fatos.
    var terminal = caller.FindFirst(ApiAuth.TerminalClaim)?.Value;
    if (terminal != ApiAuth.AnyTerminal && request.Events.Any(e => !string.Equals(e.TerminalId, terminal, StringComparison.OrdinalIgnoreCase)))
        return Results.Problem($"Eventos de outro caixa enviados por {terminal}.", statusCode: StatusCodes.Status403Forbidden);

    var accepted = await store.AppendAsync(request.Events, ct);
    return Results.Ok(new PushEventsResponse(accepted));
})
    .WithName("PushEvents")
    .WithSummary("Caixa envia um lote de eventos da outbox")
    .WithDescription("Grava com ON CONFLICT (event_id) DO NOTHING e alimenta as projeções na mesma transação. " +
                     "Devolve todos os event_id aceitos, inclusive os que já existiam: reenviar é sempre seguro.")
    .Produces<PushEventsResponse>()
    .Produces<string>(StatusCodes.Status400BadRequest);

// Servidor → caixas
sync.MapGet(SyncRoutes.Catalog, (long since, CatalogService catalog, CancellationToken ct) =>
    catalog.GetDeltaAsync(since, ct))
    .WithName("GetCatalogDelta")
    .WithSummary("Delta do catálogo depois da versão do caixa")
    .WithDescription($"Páginas de {CatalogService.DeltaPageSize} alterações. Pede snapshot (snapshotRequired) se o caixa é novo, " +
                     $"está à frente do servidor ou ficou mais de {CatalogService.MaxDeltaDistance} versões para trás.");

sync.MapGet(SyncRoutes.CatalogSnapshot, (CatalogService catalog, CancellationToken ct) =>
    catalog.GetSnapshotAsync(ct))
    .WithName("GetCatalogSnapshot")
    .WithSummary("Catálogo completo e preços agendados ainda válidos");

// Back-office (mínimo, até existir a interface web)
var backoffice = app.MapGroup("/backoffice").WithTags("Back-office").RequireAuthorization(ApiAuth.BackofficeApiPolicy);

backoffice.MapGet("/products", (CatalogService catalog, CancellationToken ct) => catalog.ListProductsAsync(ct))
    .WithName("ListProducts")
    .WithSummary("Lista o cadastro atual de produtos");

backoffice.MapPut("/products/{id:guid}", async (Guid id, ProductData product, CatalogService catalog, CancellationToken ct) =>
{
    if (id != product.Id) return Results.BadRequest("Id da rota difere do corpo.");
    var version = await catalog.UpsertProductAsync(product, ct);
    return Results.Ok(new CatalogVersionResponse(version));
})
    .WithName("UpsertProduct")
    .WithSummary("Cria ou altera um produto")
    .WithDescription("Gera uma linha em catalog_changes com nova versão. Exclusão é lógica: active = false.")
    .Produces<CatalogVersionResponse>()
    .Produces<string>(StatusCodes.Status400BadRequest);

backoffice.MapPost("/scheduled-prices", async (ScheduledPriceData price, CatalogService catalog, CancellationToken ct) =>
{
    var version = await catalog.SchedulePriceAsync(price, ct);
    return Results.Ok(new CatalogVersionResponse(version));
})
    .WithName("SchedulePrice")
    .WithSummary("Agenda um preço")
    .WithDescription("Os caixas recebem antes e aplicam sozinhos em effectiveAt, mesmo offline.")
    .Produces<CatalogVersionResponse>();

backoffice.MapPost("/projections/rebuild", async (EventStore store, CancellationToken ct) =>
{
    await store.RebuildProjectionsAsync(ct);
    return Results.NoContent();
})
    .WithName("RebuildProjections")
    .WithSummary("Apaga as projeções e relê todo o log de eventos")
    .Produces(StatusCodes.Status204NoContent);

app.Run();

public sealed record CatalogVersionResponse(long Version);

public partial class Program;
