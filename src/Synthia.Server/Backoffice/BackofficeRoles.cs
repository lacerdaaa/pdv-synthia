namespace Synthia.Server.Backoffice;

public static class BackofficeRoles
{
    public const string Admin = "admin";
    public const string Manager = "gerente";
    public const string Stock = "estoquista";

    public static readonly string[] All = [Admin, Manager, Stock];

    /// <summary>Quem mexe em cadastro e preço de produto.</summary>
    public const string CatalogEditors = $"{Admin},{Manager}";

    public static string Label(string role) => role switch
    {
        Admin => "Administrador",
        Manager => "Gerente",
        Stock => "Estoquista",
        _ => role,
    };
}
