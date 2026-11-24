using System.Data;
using System.Data.Common;
using CvErp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CvErp.Infrastructure.Services;

/// <summary>Sinh số chứng từ tuần tự, an toàn khi nhiều người thao tác đồng thời.</summary>
public class NumberSequenceService
{
    private readonly AppDbContext _db;

    public NumberSequenceService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<string> NextAsync(string prefix, DateOnly date)
    {
        var year = date.Year;
        var connection = _db.Database.GetDbConnection();
        var mustClose = connection.State != ConnectionState.Open;

        if (mustClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _db.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                IF NOT EXISTS (SELECT 1 FROM NumberSequences WHERE Prefix = @prefix AND Year = @year)
                    INSERT INTO NumberSequences (Prefix, Year, LastNumber) VALUES (@prefix, @year, 0);

                UPDATE NumberSequences
                SET LastNumber = LastNumber + 1
                OUTPUT INSERTED.LastNumber
                WHERE Prefix = @prefix AND Year = @year;
                """;

            var pPrefix = command.CreateParameter();
            pPrefix.ParameterName = "@prefix";
            pPrefix.Value = prefix;
            command.Parameters.Add(pPrefix);

            var pYear = command.CreateParameter();
            pYear.ParameterName = "@year";
            pYear.Value = year;
            command.Parameters.Add(pYear);

            var result = await command.ExecuteScalarAsync();
            var next = Convert.ToInt32(result);
            return $"{prefix}-{year}-{next:D4}";
        }
        finally
        {
            if (mustClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}
