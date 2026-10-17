using System.Globalization;
using Synthia.Contracts.Events;

namespace Synthia.Server.Retaguarda;

/// <summary>Formatos pt-BR da retaguarda.</summary>
public static class Fmt
{
    public static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string Money(long cents) => (cents / 100m).ToString("C", PtBr);

    public static string Qty(decimal q) => q == decimal.Truncate(q) ? q.ToString("N0", PtBr) : q.ToString("N3", PtBr);

    public static string When(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime().ToString("dd/MM/yyyy HH:mm", PtBr);

    public static string Source(string source) => source switch
    {
        StockReceiptSources.Transfer => "Transferência da loja antiga",
        StockReceiptSources.Supplier => "Fornecedor",
        _ => "Outra",
    };

    public static string Reason(string reason) => reason switch
    {
        AdjustmentReasons.Damage => "Quebra / avaria",
        AdjustmentReasons.Expired => "Vencimento",
        AdjustmentReasons.Consumption => "Consumo interno",
        AdjustmentReasons.Theft => "Furto / extravio",
        AdjustmentReasons.SupplierReturn => "Devolução ao fornecedor",
        _ => "Outro",
    };

    public static string Movement(string kind) => kind switch
    {
        "sale" => "Venda",
        "received" => "Entrada",
        "count" => "Contagem",
        "item_cancelled" => "Item cancelado",
        "sale_cancelled" => "Venda cancelada",
        "adjusted" => "Perda / ajuste",
        _ => kind,
    };
}
