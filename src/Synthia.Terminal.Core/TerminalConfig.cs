using System.Text.Json;
using System.Text.Json.Serialization;
using Synthia.Terminal.Core.Catalog;

namespace Synthia.Terminal.Core;

/// <summary>
/// Configuração de um caixa (<c>terminal.json</c>). Os valores padrão seguem o mais comum no varejo;
/// cada loja ajusta portas, modelos e o formato da etiqueta da balança.
/// </summary>
public sealed record TerminalConfig
{
    public StoreSection Store { get; init; } = new();
    public TerminalSection Terminal { get; init; } = new();
    public NfceSection Nfce { get; init; } = new();
    public ScaleLabelSection ScaleLabel { get; init; } = new();
    public DevicesSection Devices { get; init; } = new();

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static TerminalConfig Load(string path) =>
        JsonSerializer.Deserialize<TerminalConfig>(File.ReadAllText(path), JsonOptions)
        ?? throw new InvalidOperationException($"{path} vazio.");

    public static TerminalConfig Parse(string json) =>
        JsonSerializer.Deserialize<TerminalConfig>(json, JsonOptions) ?? throw new InvalidOperationException("Configuração vazia.");

    public TerminalOptions ToOptions() => new(Terminal.Id, Terminal.DatabasePath, new Uri(Terminal.ServerUrl));

    public ScaleBarcodeFormat ToScaleBarcodeFormat() => new(ScaleLabel.CodeLength, ScaleLabel.ValueLength, ScaleLabel.Value);

    /// <summary>Lista os problemas encontrados; vazia quando o caixa pode subir.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (new string(Store.Cnpj.Where(char.IsAsciiDigit).ToArray()).Length != 14)
            errors.Add("store.cnpj deve ter 14 dígitos.");
        if (!System.Text.RegularExpressions.Regex.IsMatch(Terminal.Id, "^CX[0-9]{2}$"))
            errors.Add("terminal.id deve seguir CX01, CX02...");
        if (!Uri.TryCreate(Terminal.ServerUrl, UriKind.Absolute, out _))
            errors.Add("terminal.serverUrl inválida.");
        if (string.IsNullOrWhiteSpace(Terminal.DatabasePath))
            errors.Add("terminal.databasePath obrigatório.");
        if (Nfce.Serie is < 1 or > 889)
            errors.Add("nfce.serie deve estar entre 1 e 889 (série normal de NFC-e).");
        if (ScaleLabel.CodeLength + ScaleLabel.ValueLength != 11)
            errors.Add("scaleLabel: codeLength + valueLength precisa ser 11.");

        if (Devices.Scanner.Mode == ScannerMode.Serial && string.IsNullOrWhiteSpace(Devices.Scanner.Port))
            errors.Add("devices.scanner.port obrigatório no modo serial.");
        if (Devices.Scale.Model == ScaleModel.Toledo && string.IsNullOrWhiteSpace(Devices.Scale.Port))
            errors.Add("devices.scale.port obrigatório para balança Toledo.");
        if (Devices.Printer.Connection is PrinterConnection.Network or PrinterConnection.Serial or PrinterConnection.Device
            && string.IsNullOrWhiteSpace(Devices.Printer.Address))
            errors.Add("devices.printer.address obrigatório (host:9100, porta serial ou caminho do dispositivo).");
        if (Devices.Printer.Columns is not (32 or 42 or 48 or 64))
            errors.Add("devices.printer.columns: use 48 (80 mm) ou 32 (58 mm).");

        return errors;
    }
}

/// <summary>
/// Dados impressos no cupom. O CNPJ daqui é o do certificado e compõe a chave da NFC-e; nome, endereço,
/// IE e rodapé vêm do cadastro da loja na retaguarda quando o caixa já sincronizou (estes são o fallback).
/// </summary>
public sealed record StoreSection
{
    public string Name { get; init; } = "Minha Loja";
    public string Cnpj { get; init; } = "00.000.000/0001-91";
    public string Address { get; init; } = "";
    public string StateRegistration { get; init; } = "";
    public string ReceiptFooter { get; init; } = "";
}

public sealed record TerminalSection
{
    public string Id { get; init; } = "CX01";
    public string ServerUrl { get; init; } = "http://192.168.10.2:5290";
    public string DatabasePath { get; init; } = "caixa.db";

    /// <summary>
    /// Chave deste caixa no servidor (Sync:Terminals:CX01 lá). Prefira a variável de ambiente
    /// SYNTHIA_TERMINAL_KEY a gravar no arquivo.
    /// </summary>
    public string? ApiKey { get; init; }

    public string? ResolveApiKey() =>
        Environment.GetEnvironmentVariable("SYNTHIA_TERMINAL_KEY") is { Length: > 0 } fromEnv ? fromEnv : ApiKey;
}

public enum NfceEnvironment { Homologacao, Producao }

public sealed record NfceSection
{
    /// <summary>Uma série por caixa: CX01 = 1, CX02 = 2...</summary>
    public int Serie { get; init; } = 1;
    public NfceEnvironment Ambiente { get; init; } = NfceEnvironment.Homologacao;
    /// <summary>Certificado A1 (.pfx). A senha vem da variável de ambiente SYNTHIA_CERT_PASSWORD, nunca do arquivo.</summary>
    public string? CertificatePath { get; init; }
}

public sealed record ScaleLabelSection
{
    public int CodeLength { get; init; } = 6;
    public int ValueLength { get; init; } = 5;
    public ScaleLabelValue Value { get; init; } = ScaleLabelValue.TotalPrice;
}

public sealed record DevicesSection
{
    public ScannerSection Scanner { get; init; } = new();
    public ScaleSection Scale { get; init; } = new();
    public PrinterSection Printer { get; init; } = new();
    public PaymentTerminalSection PaymentTerminal { get; init; } = new();
}

public enum ScannerMode { Keyboard, Serial, Simulated }

public sealed record ScannerSection
{
    /// <summary>Keyboard (HID) é como o leitor sai de fábrica; Serial é mais robusto.</summary>
    public ScannerMode Mode { get; init; } = ScannerMode.Keyboard;
    public string? Port { get; init; }
    public int BaudRate { get; init; } = 9600;
}

public enum ScaleModel { None, Toledo, Simulated }

public sealed record ScaleSection
{
    public ScaleModel Model { get; init; } = ScaleModel.None;
    public string? Port { get; init; }
    public int BaudRate { get; init; } = 9600;
}

public enum PrinterConnection { Network, Serial, Device, Simulated }

public sealed record PrinterSection
{
    public PrinterConnection Connection { get; init; } = PrinterConnection.Simulated;
    /// <summary>host:porta (rede), COM3 (serial) ou /dev/usb/lp0 (dispositivo).</summary>
    public string? Address { get; init; }
    public int Columns { get; init; } = 48;
    public int BaudRate { get; init; } = 9600;
}

public enum PaymentProvider { Simulated, Sitef, Paygo }

public sealed record PaymentTerminalSection
{
    public PaymentProvider Provider { get; init; } = PaymentProvider.Simulated;
}
