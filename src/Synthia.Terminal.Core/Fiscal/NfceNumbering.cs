using Synthia.Terminal.Core.Storage;

namespace Synthia.Terminal.Core.Fiscal;

/// <summary>
/// Próximo número de NFC-e da série deste caixa, guardado no SQLite local. Cada caixa tem sua série,
/// então caixas nunca disputam número, nem offline. Números pulados são inutilizados no fim do dia.
/// </summary>
public sealed class NfceNumbering(LocalDb db, int serie)
{
    public int Serie => serie;

    public long Next()
    {
        using var conn = db.Open();
        using var tx = conn.BeginTransaction();

        var key = $"nfce_next_number_s{serie}";
        var next = long.TryParse(Meta.Get(conn, key, tx), out var n) ? n : 1;
        Meta.Set(conn, key, (next + 1).ToString(), tx);

        tx.Commit();
        return next;
    }
}
