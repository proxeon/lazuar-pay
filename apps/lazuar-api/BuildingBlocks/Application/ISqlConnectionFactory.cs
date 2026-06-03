using System.Data;

namespace BuildingBlocks.Application;

public interface ISqlConnectionFactory
{
    IDbConnection CreateConnection();
}
