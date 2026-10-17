using System.Text.Json;
using System.Text.Json.Nodes;

namespace Synthia.Simulator;

public sealed record Exchange(string Actor, string Method, string Path, string? Request, int? Status, string? Response, string? Note);

public sealed record Step(
    int Number,
    string Phase,
    string Situation,
    IReadOnlyList<string> Routes,
    IReadOnlyList<string> Process,
    IReadOnlyList<Exchange> Exchanges,
    IReadOnlyList<Change> Effects,
    IReadOnlyList<string> Notes);

public sealed record Change(string Key, string? Before, string? After);

/// <summary>Acumula as trocas HTTP do passo em andamento.</summary>
public sealed class Recorder
{
    private readonly List<Exchange> _current = [];
    private readonly Lock _gate = new();

    public void Add(Exchange e)
    {
        lock (_gate) _current.Add(e);
    }

    public IReadOnlyList<Exchange> Drain()
    {
        lock (_gate)
        {
            var list = _current.ToList();
            _current.Clear();
            return list;
        }
    }

    public static string? Pretty(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            var text = JsonNode.Parse(body)!.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            return text.Length > 4000 ? text[..4000] + "\n…" : text;
        }
        catch (JsonException)
        {
            return body;
        }
    }
}

public enum LinkState { Online, Offline, DropNextResponse }

/// <summary>
/// Rede simulada entre um ator e o servidor: grava cada requisição/resposta e permite
/// derrubar o link ou perder uma resposta depois que o servidor já processou.
/// </summary>
public sealed class RecordingLink(string actor, Recorder recorder) : DelegatingHandler(new HttpClientHandler())
{
    public LinkState State { get; set; } = LinkState.Online;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.PathAndQuery;
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);

        if (State == LinkState.Offline)
        {
            recorder.Add(new Exchange(actor, request.Method.Method, path, Recorder.Pretty(body), null, null, "sem conexão com o servidor"));
            throw new HttpRequestException("sem conexão com o servidor (simulado)");
        }

        var response = await base.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);

        if (State == LinkState.DropNextResponse)
        {
            State = LinkState.Online;
            recorder.Add(new Exchange(actor, request.Method.Method, path, Recorder.Pretty(body), (int)response.StatusCode,
                Recorder.Pretty(text), "o servidor processou, mas a resposta se perdeu no caminho"));
            throw new HttpRequestException("resposta perdida (simulado)");
        }

        recorder.Add(new Exchange(actor, request.Method.Method, path, Recorder.Pretty(body), (int)response.StatusCode, Recorder.Pretty(text), null));
        return response;
    }
}
