namespace CvErp.Infrastructure.Common;

public sealed record UserContext(string UserId, string UserName, string? IpAddress = null);

public class BusinessRuleException : Exception
{
    public BusinessRuleException(string message) : base(message)
    {
    }
}

public static class DocumentPrefixes
{
    public const string Receipt = "PN";
    public const string Issue = "PX";
    public const string Transfer = "DC";
    public const string ProductionOrder = "LSX";
    public const string MaterialIssue = "CCP";
}
