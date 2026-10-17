namespace Synthia.Terminal.Core.Storage;

/// <summary>Avisa o worker de sync que há evento novo, para enviar logo após a venda.</summary>
public interface IOutboxSignal
{
    void Notify();
}
