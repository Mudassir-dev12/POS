using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.Data.Services;

public class NumberSequenceService : INumberSequenceService
{
    private readonly AppDbContext _context;

    public NumberSequenceService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<string> GetNextNumberAsync(string prefix, CancellationToken cancellationToken = default)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await _context.Database.OpenConnectionAsync(cancellationToken);
        }

        using var cmd = connection.CreateCommand();
        cmd.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        cmd.CommandText = @"
            INSERT INTO NumberSequences ([Key], [LastNumber]) VALUES (@key, 1)
            ON CONFLICT([Key]) DO UPDATE SET [LastNumber] = [LastNumber] + 1
            RETURNING [LastNumber];";

        var param = cmd.CreateParameter();
        param.ParameterName = "@key";
        param.Value = prefix;
        cmd.Parameters.Add(param);

        var scalar = await cmd.ExecuteScalarAsync(cancellationToken);
        long nextNum = Convert.ToInt64(scalar);
        return $"{prefix}-{nextNum:D6}";
    }
}

