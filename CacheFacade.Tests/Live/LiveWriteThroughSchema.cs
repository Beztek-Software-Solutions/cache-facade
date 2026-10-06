// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests.Live
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using Beztek.Facade.Sql;
    using SqlDbType = Beztek.Facade.Sql.DbType;

    /// <summary>Creates and resets <c>test_etag_cacheable</c> for live WriteThrough SQL engines.</summary>
    public static class LiveWriteThroughSchema
    {
        public static void Ensure(ISqlFacade sql)
        {
            foreach (string ddl in CreateStatements(sql.GetSqlFacadeConfig().DbType))
                ExecuteRaw(sql, ddl);
        }

        public static void Reset(ISqlFacade sql)
        {
            SqlDbType dbType = sql.GetSqlFacadeConfig().DbType;
            try
            {
                ExecuteRaw(sql, DeleteStatement(dbType));
            }
            catch (Exception)
            {
                // Table may not exist yet on first run.
            }
        }

        public static int CountRows(ISqlFacade sql, string id)
        {
            string escaped = (id ?? string.Empty).Replace("'", "''", StringComparison.Ordinal);
            using IDbConnection con = sql.GetSqlFacadeConfig().GetConnection();
            using IDbCommand cmd = con.CreateCommand();
            cmd.CommandText = sql.GetSqlFacadeConfig().DbType switch
            {
                SqlDbType.SQLSERVER => $"SELECT COUNT(1) FROM dbo.test_etag_cacheable WHERE id = '{escaped}'",
                SqlDbType.ORACLE => $"SELECT COUNT(1) FROM \"test_etag_cacheable\" WHERE \"id\" = '{escaped}'",
                _ => $"SELECT COUNT(1) FROM test_etag_cacheable WHERE id = '{escaped}'",
            };
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        private static IEnumerable<string> CreateStatements(SqlDbType dbType) => dbType switch
        {
            SqlDbType.POSTGRES => new[]
            {
                @"CREATE TABLE IF NOT EXISTS test_etag_cacheable (
                    id TEXT PRIMARY KEY,
                    value TEXT,
                    created_date TIMESTAMP,
                    updated_date TIMESTAMP,
                    etag TEXT,
                    is_deleted INT NOT NULL DEFAULT 0)",
            },
            SqlDbType.SQLSERVER => new[]
            {
                @"IF OBJECT_ID('dbo.test_etag_cacheable', 'U') IS NULL
                  CREATE TABLE dbo.test_etag_cacheable (
                    id NVARCHAR(64) NOT NULL PRIMARY KEY,
                    value NVARCHAR(256) NULL,
                    created_date DATETIME2 NULL,
                    updated_date DATETIME2 NULL,
                    etag NVARCHAR(64) NULL,
                    is_deleted INT NOT NULL CONSTRAINT DF_test_etag_cacheable_is_deleted DEFAULT (0))",
            },
            SqlDbType.MYSQL or SqlDbType.MARIADB => new[]
            {
                @"CREATE TABLE IF NOT EXISTS test_etag_cacheable (
                    id VARCHAR(64) PRIMARY KEY,
                    value VARCHAR(256),
                    created_date DATETIME(6),
                    updated_date DATETIME(6),
                    etag VARCHAR(64),
                    is_deleted INT NOT NULL DEFAULT 0)",
            },
            SqlDbType.ORACLE => new[]
            {
                """BEGIN EXECUTE IMMEDIATE 'DROP TABLE "test_etag_cacheable" CASCADE CONSTRAINTS'; EXCEPTION WHEN OTHERS THEN NULL; END;""",
                """BEGIN EXECUTE IMMEDIATE 'DROP TABLE test_etag_cacheable CASCADE CONSTRAINTS'; EXCEPTION WHEN OTHERS THEN NULL; END;""",
                """BEGIN EXECUTE IMMEDIATE 'CREATE TABLE "test_etag_cacheable" ("id" VARCHAR2(64) PRIMARY KEY, "value" VARCHAR2(256), "created_date" TIMESTAMP, "updated_date" TIMESTAMP, "etag" VARCHAR2(64), "is_deleted" NUMBER(1) DEFAULT 0 NOT NULL)'; END;""",
            },
            _ => throw new ArgumentOutOfRangeException(nameof(dbType), dbType, "SQLite is not used for live ambient WriteThrough tests."),
        };

        private static string DeleteStatement(SqlDbType dbType) => dbType switch
        {
            SqlDbType.SQLSERVER => "DELETE FROM dbo.test_etag_cacheable",
            SqlDbType.ORACLE => "DELETE FROM \"test_etag_cacheable\"",
            _ => "DELETE FROM test_etag_cacheable",
        };

        private static void ExecuteRaw(ISqlFacade sql, string commandText)
        {
            using IDbConnection con = sql.GetSqlFacadeConfig().GetConnection();
            using IDbCommand cmd = con.CreateCommand();
            cmd.CommandText = commandText;
            cmd.ExecuteNonQuery();
        }
    }
}
