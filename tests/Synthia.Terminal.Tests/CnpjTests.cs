using Synthia.Contracts;

namespace Synthia.Terminal.Tests;

public sealed class CnpjTests
{
    [Theory]
    [InlineData("11.222.333/0001-81", true)]
    [InlineData("11222333000181", true)]
    [InlineData("11.222.333/0001-82", false)]
    [InlineData("11.111.111/1111-11", false)]
    [InlineData("1122233300018", false)]
    [InlineData("", false)]
    public void Valida_digitos_verificadores(string value, bool valid) => Assert.Equal(valid, Cnpj.IsValid(value));

    [Fact]
    public void Formata_com_mascara_e_extrai_digitos()
    {
        Assert.Equal("11.222.333/0001-81", Cnpj.Format("11222333000181"));
        Assert.Equal("11222333000181", Cnpj.Digits("11.222.333/0001-81"));
    }
}
