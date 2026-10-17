using System.Text.Json;

namespace Synthia.Contracts.Events;

/// <summary>
/// Fato imutável gerado no caixa. É a unidade de sincronização caixa → servidor → nuvem.
/// </summary>
/// <param name="EventId">UUIDv7 gerado na origem; chave de idempotência.</param>
/// <param name="StreamId">A venda, o turno ou o item afetado.</param>
/// <param name="StreamSeq">Ordem do evento dentro do stream (1, 2, 3...).</param>
/// <param name="Type">Um dos valores de <see cref="EventTypes"/>.</param>
/// <param name="TerminalId">Caixa de origem (CX01...CX04).</param>
/// <param name="OccurredAt">Relógio do caixa, sincronizado via NTP do servidor.</param>
/// <param name="CatalogVersion">Versão do catálogo usada no momento do fato.</param>
/// <param name="Payload">Corpo específico do tipo (itens, pagamentos, chave da NFC-e...).</param>
public sealed record EventEnvelope(
    Guid EventId,
    Guid StreamId,
    int StreamSeq,
    string Type,
    string TerminalId,
    DateTimeOffset OccurredAt,
    long CatalogVersion,
    JsonElement Payload);
