namespace CvErp.Domain.Constants;

public static class AppRoles
{
    public const string Administrator = "Administrator";
    public const string Manager = "Manager";
    public const string Warehouse = "Warehouse";
    public const string Production = "Production";
    public const string Purchasing = "Purchasing";

    public static readonly string[] All =
    {
        Administrator, Manager, Warehouse, Production, Purchasing
    };
}
