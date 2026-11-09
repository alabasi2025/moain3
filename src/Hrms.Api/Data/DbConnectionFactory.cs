using System.Data;
using Microsoft.Data.SqlClient;

namespace Hrms.Api.Data;

public interface IDbConnectionFactory
{
    IDbConnection Create();
}

public sealed class SqlConnectionFactory(IConfiguration cfg) : IDbConnectionFactory
{
    private readonly string _cs = cfg.GetConnectionString("Hrms")
        ?? throw new InvalidOperationException("ConnectionStrings:Hrms is not configured");

    public IDbConnection Create() => new SqlConnection(_cs);
}
