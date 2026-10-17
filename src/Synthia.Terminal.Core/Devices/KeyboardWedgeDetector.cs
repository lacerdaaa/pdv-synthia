namespace Synthia.Terminal.Core.Devices;

/// <summary>
/// Leitor em modo teclado (HID) "digita" o código muito mais rápido que uma pessoa e termina com Enter.
/// A interface repassa cada tecla com o horário; quando a sequência inteira veio rápida, é uma leitura.
/// </summary>
public sealed class KeyboardWedgeDetector(TimeSpan? maxInterval = null, int minLength = 4)
{
    private readonly TimeSpan _maxInterval = maxInterval ?? TimeSpan.FromMilliseconds(50);
    private readonly List<char> _buffer = [];
    private TimeSpan _last;

    /// <summary>Devolve o código quando <paramref name="key"/> é Enter e a sequência veio do leitor; senão, nulo.</summary>
    public string? Feed(char key, TimeSpan timestamp)
    {
        var fast = _buffer.Count > 0 && timestamp - _last <= _maxInterval;

        if (key is '\r' or '\n')
        {
            var code = fast && _buffer.Count >= minLength ? new string([.. _buffer]) : null;
            _buffer.Clear();
            return code;
        }

        // Pausa longa no meio: é gente digitando; a candidata a leitura recomeça nesta tecla.
        if (!fast) _buffer.Clear();

        _buffer.Add(key);
        _last = timestamp;
        return null;
    }
}
