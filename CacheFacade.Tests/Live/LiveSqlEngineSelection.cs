// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests.Live
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Beztek.Facade.Sql;

    /// <summary>
    /// Selects which SQL engines run live WriteThrough ambient-transaction tests via
    /// <c>CACHEFACADE_LIVE_SQL_ENGINES</c>.
    /// <para>
    /// Examples:
    /// <list type="bullet">
    /// <item><c>postgres</c> — one engine</item>
    /// <item><c>mysql,mariadb</c> — subset</item>
    /// <item><c>all</c> — every enlisting engine (Postgres, SQL Server, MySQL, MariaDB, Oracle).
    /// SQLite is never included — it does not support ambient <c>TransactionScope</c> enlistment.</item>
    /// </list>
    /// Unset / empty → no live SQL fixtures are registered (default unit CI stays fast and Docker-free).
    /// </para>
    /// </summary>
    public static class LiveSqlEngineSelection
    {
        public const string EnvVar = "CACHEFACADE_LIVE_SQL_ENGINES";

        /// <summary>Engines that enlist in ambient <see cref="System.Transactions.TransactionScope"/>.</summary>
        private static readonly DbType[] EnlistingEngines =
        {
            DbType.POSTGRES,
            DbType.SQLSERVER,
            DbType.MYSQL,
            DbType.MARIADB,
            DbType.ORACLE,
        };

        /// <summary>True when the env var is set to a non-empty value.</summary>
        public static bool IsConfigured =>
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvVar));

        /// <summary>Engines requested for this process; empty when live SQL tests should not run.</summary>
        public static IReadOnlyList<DbType> Resolve()
        {
            string raw = Environment.GetEnvironmentVariable(EnvVar);
            if (string.IsNullOrWhiteSpace(raw))
                return Array.Empty<DbType>();

            var selected = new List<DbType>();
            foreach (string token in raw.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string t = token.Trim().ToLowerInvariant();
                if (t is "all" or "*")
                    return EnlistingEngines.ToList();

                if (t is "sqlite")
                {
                    throw new ArgumentException(
                        $"SQLite is not supported for {EnvVar} ambient WriteThrough tests " +
                        "(Microsoft.Data.Sqlite does not enlist in TransactionScope). " +
                        "Use: all | postgres | sqlserver | mysql | mariadb | oracle.");
                }

                if (TryParse(t, out DbType dbType) && !selected.Contains(dbType))
                    selected.Add(dbType);
                else
                    throw new ArgumentException(
                        $"Unknown engine '{token}' in {EnvVar}. " +
                        "Use: all | postgres | sqlserver | mysql | mariadb | oracle " +
                        "(comma-separated for a subset). SQLite is not supported.");
            }

            return selected;
        }

        public static bool TryParse(string token, out DbType dbType)
        {
            switch (token.Trim().ToLowerInvariant())
            {
                case "postgres":
                case "postgresql":
                case "pg":
                    dbType = DbType.POSTGRES;
                    return true;
                case "sqlserver":
                case "mssql":
                case "sql":
                    dbType = DbType.SQLSERVER;
                    return true;
                case "mysql":
                    dbType = DbType.MYSQL;
                    return true;
                case "mariadb":
                case "maria":
                    dbType = DbType.MARIADB;
                    return true;
                case "oracle":
                case "ora":
                    dbType = DbType.ORACLE;
                    return true;
                default:
                    dbType = default;
                    return false;
            }
        }
    }
}
