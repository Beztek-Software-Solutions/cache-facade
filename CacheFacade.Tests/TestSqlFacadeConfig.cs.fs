// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using System.Data;

    using Beztek.Facade.Sql;

    public class TestSqlFacadeConfig : SqlFacadeConfig
    {
        private IDbConnection connection;

        public TestSqlFacadeConfig(Beztek.Facade.Sql.DbType dbType, string connectionString)
            : base(dbType, connectionString) { }

        public override IDbConnection GetConnection()
        {
            if (this.DbType == Beztek.Facade.Sql.DbType.SQLITE)
            {
                if (this.connection == null)
                {
                    this.connection = new TestSqliteConnection(this.ConnectionString);
                }

                this.connection.Open();
                return this.connection;
            }
            return default(IDbConnection);
        }
    }
}
