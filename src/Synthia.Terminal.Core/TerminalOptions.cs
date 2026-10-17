namespace Synthia.Terminal.Core;

/// <param name="TerminalId">Identificador do caixa (CX01...CX04).</param>
/// <param name="DatabasePath">Arquivo SQLite local.</param>
/// <param name="ServerUrl">Servidor da loja; pode estar fora do ar sem afetar a venda.</param>
public sealed record TerminalOptions(string TerminalId, string DatabasePath, Uri ServerUrl);
