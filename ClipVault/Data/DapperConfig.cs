using Dapper;
using System.Data;

namespace ClipVault.Data;

/// <summary>
/// Configures Dapper type handlers for SQLite compatibility.
/// </summary>
public static class DapperConfig
{
    private static bool _initialized;
    
    /// <summary>
    /// Initializes Dapper type handlers. Safe to call multiple times.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;
        
        SqlMapper.AddTypeHandler(new GuidTypeHandler());
        SqlMapper.AddTypeHandler(new DateTimeTypeHandler());
        SqlMapper.AddTypeHandler(new NullableGuidTypeHandler());
        
        _initialized = true;
    }
}

/// <summary>
/// Handles GUID conversion for SQLite (stored as TEXT).
/// </summary>
public class GuidTypeHandler : SqlMapper.TypeHandler<Guid>
{
    public override Guid Parse(object value)
    {
        if (value is string str)
        {
            return Guid.Parse(str);
        }
        return Guid.Empty;
    }

    public override void SetValue(IDbDataParameter parameter, Guid value)
    {
        parameter.Value = value.ToString();
    }
}

/// <summary>
/// Handles nullable GUID conversion for SQLite.
/// </summary>
public class NullableGuidTypeHandler : SqlMapper.TypeHandler<Guid?>
{
    public override Guid? Parse(object value)
    {
        if (value == null || value == DBNull.Value)
        {
            return null;
        }
        if (value is string str && Guid.TryParse(str, out var guid))
        {
            return guid;
        }
        return null;
    }

    public override void SetValue(IDbDataParameter parameter, Guid? value)
    {
        parameter.Value = value?.ToString() ?? (object)DBNull.Value;
    }
}

/// <summary>
/// Handles DateTime conversion for SQLite (stored as ISO 8601 TEXT).
/// </summary>
public class DateTimeTypeHandler : SqlMapper.TypeHandler<DateTime>
{
    public override DateTime Parse(object value)
    {
        if (value is string str)
        {
            return DateTime.Parse(str, null, System.Globalization.DateTimeStyles.RoundtripKind);
        }
        if (value is DateTime dt)
        {
            return dt;
        }
        return DateTime.MinValue;
    }

    public override void SetValue(IDbDataParameter parameter, DateTime value)
    {
        parameter.Value = value.ToString("O");
    }
}
