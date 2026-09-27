// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using System.Data;

    using Beztek.Facade.Sql;

    public class TestSqlFacadeConfig : SqlFacadeConfig
    {
        public TestSqlFacadeConfig(Beztek.Facade.Sql.DbType dbType, string connectionString)
            : base(dbType, connectionString) { }

        public override IDbConnection GetConnection()
            => base.GetConnection();
    }
}
