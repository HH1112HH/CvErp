using CvErp.Domain.Entities;
using CvErp.Infrastructure.Common;
using CvErp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CvErp.Infrastructure.Services;

public class AuditService
{
    private readonly AppDbContext _db;

    public AuditService(AppDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(UserContext user, string action, string entityName, string? entityId, string? description)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            Timestamp = DateTime.UtcNow,
            UserId = user.UserId,
            UserName = user.UserName,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Description = description,
            IpAddress = user.IpAddress
        });
        await _db.SaveChangesAsync();
    }
}
