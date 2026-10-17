using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace Synthia.Server.Backoffice;

/// <param name="Code">cProd: código do produto no fornecedor.</param>
/// <param name="Ean">cEAN da unidade comercial (nulo para "SEM GTIN").</param>
/// <param name="TaxEan">cEANTrib da unidade tributável, normalmente a unidade de venda no varejo.</param>
public sealed record NfeItem(
    int Number, string Code, string? Ean, string Description, string? Ncm, string Cfop,
    string Unit, decimal Quantity, decimal UnitValue, decimal TotalValue,
    string? TaxEan, string TaxUnit, decimal TaxQuantity);

public sealed record NfeDocument(
    string AccessKey, string Number, string Series, DateTimeOffset IssuedAt,
    string SupplierCnpj, string SupplierName, decimal TotalValue, IReadOnlyList<NfeItem> Items);

/// <summary>Leitura do XML da NF-e (modelo 55) do fornecedor, como baixado da SEFAZ ou recebido por e-mail.</summary>
public static class NfeXml
{
    private static readonly XNamespace Ns = "http://www.portalfiscal.inf.br/nfe";

    public static NfeDocument Parse(string xml)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml);
        }
        catch (XmlException ex)
        {
            throw new FormatException("Arquivo não é um XML válido.", ex);
        }

        var inf = doc.Descendants(Ns + "infNFe").FirstOrDefault()
                  ?? throw new FormatException("XML não é uma NF-e (infNFe não encontrado).");
        var ide = inf.Element(Ns + "ide") ?? throw new FormatException("NF-e sem ide.");
        if (Text(ide, "mod") is { } mod && mod != "55")
            throw new FormatException($"Modelo {mod} não é NF-e de entrada (esperado 55).");

        var key = doc.Descendants(Ns + "chNFe").FirstOrDefault()?.Value
                  ?? ((string?)inf.Attribute("Id"))?.Replace("NFe", "")
                  ?? "";
        if (key.Length != 44 || !key.All(char.IsAsciiDigit)) throw new FormatException("Chave de acesso inválida.");

        var emit = inf.Element(Ns + "emit") ?? throw new FormatException("NF-e sem emitente.");
        var items = inf.Elements(Ns + "det").Select(det =>
        {
            var prod = det.Element(Ns + "prod") ?? throw new FormatException("Item sem prod.");
            return new NfeItem(
                (int?)det.Attribute("nItem") ?? 0,
                Text(prod, "cProd") ?? "",
                Gtin(Text(prod, "cEAN")),
                Text(prod, "xProd") ?? "",
                Text(prod, "NCM"),
                Text(prod, "CFOP") ?? "",
                (Text(prod, "uCom") ?? "UN").ToUpperInvariant(),
                Dec(prod, "qCom"),
                Dec(prod, "vUnCom"),
                Dec(prod, "vProd"),
                Gtin(Text(prod, "cEANTrib")),
                (Text(prod, "uTrib") ?? Text(prod, "uCom") ?? "UN").ToUpperInvariant(),
                Dec(prod, "qTrib"));
        }).ToList();

        var issued = Text(ide, "dhEmi") ?? Text(ide, "dEmi");
        return new NfeDocument(
            key,
            Text(ide, "nNF") ?? "",
            Text(ide, "serie") ?? "",
            issued is null ? DateTimeOffset.MinValue : DateTimeOffset.Parse(issued, CultureInfo.InvariantCulture),
            Text(emit, "CNPJ") ?? Text(emit, "CPF") ?? "",
            Text(emit, "xFant") ?? Text(emit, "xNome") ?? "",
            inf.Element(Ns + "total")?.Element(Ns + "ICMSTot") is { } tot ? Dec(tot, "vNF") : items.Sum(i => i.TotalValue),
            items);
    }

    private static string? Text(XElement parent, string name) => parent.Element(Ns + name)?.Value.Trim();

    private static decimal Dec(XElement parent, string name) =>
        decimal.TryParse(Text(parent, name), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : 0;

    private static string? Gtin(string? value) =>
        value is null || value.Length == 0 || !value.All(char.IsAsciiDigit) ? null : value; // "SEM GTIN" → nulo
}
