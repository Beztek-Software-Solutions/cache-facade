// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests.Live
{
    using System;
    using System.Threading.Tasks;
    using Beztek.Facade.Sql;
    using DotNet.Testcontainers.Containers;
    using Testcontainers.MariaDb;
    using Testcontainers.MsSql;
    using Testcontainers.MySql;
    using Testcontainers.Oracle;
    using Testcontainers.PostgreSql;
    using SqlDbType = Beztek.Facade.Sql.DbType;

    /// <summary>
    /// Starts a throwaway SQL engine for live WriteThrough ambient-transaction tests
    /// (Testcontainers). SQLite is not supported here.
    /// </summary>
    public sealed class LiveSqlEngineHost : IAsyncDisposable
    {
        private readonly IContainer _container;

        private LiveSqlEngineHost(SqlDbType dbType, ISqlFacade sql, IContainer container)
        {
            DbType = dbType;
            Sql = sql;
            _container = container;
        }

        public SqlDbType DbType { get; }

        public ISqlFacade Sql { get; }

        public static async Task<LiveSqlEngineHost> StartAsync(SqlDbType dbType)
        {
            if (dbType == SqlDbType.SQLITE)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(dbType),
                    dbType,
                    "SQLite does not support ambient TransactionScope enlistment.");
            }

            LiveContainerRuntime.EnsureConfigured();

            try
            {
                return dbType switch
                {
                    SqlDbType.POSTGRES => await StartPostgresAsync().ConfigureAwait(false),
                    SqlDbType.SQLSERVER => await StartSqlServerAsync().ConfigureAwait(false),
                    SqlDbType.MYSQL => await StartMySqlAsync().ConfigureAwait(false),
                    SqlDbType.MARIADB => await StartMariaDbAsync().ConfigureAwait(false),
                    SqlDbType.ORACLE => await StartOracleAsync().ConfigureAwait(false),
                    _ => throw new ArgumentOutOfRangeException(nameof(dbType), dbType, "Unsupported engine"),
                };
            }
            catch (Exception ex) when (IsDockerUnavailable(ex))
            {
                throw new InvalidOperationException(
                    $"Cannot start {dbType}: no container engine API available. " +
                    "Install Podman (preferred) or Docker, ensure the engine is running, " +
                    $"then re-run with {LiveSqlEngineSelection.EnvVar} set.",
                    ex);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_container != null)
                await _container.DisposeAsync().ConfigureAwait(false);
        }

        private static async Task<LiveSqlEngineHost> StartPostgresAsync()
        {
            PostgreSqlContainer container = new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("cachefacade")
                .WithUsername("cachefacade")
                .WithPassword("cachefacade")
                .Build();
            await container.StartAsync().ConfigureAwait(false);
            return CreateHost(SqlDbType.POSTGRES, container.GetConnectionString(), container);
        }

        private static async Task<LiveSqlEngineHost> StartSqlServerAsync()
        {
            MsSqlContainer container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
                .WithPassword("CacheFacade_Test_1!")
                .Build();
            await container.StartAsync().ConfigureAwait(false);
            return CreateHost(SqlDbType.SQLSERVER, container.GetConnectionString(), container);
        }

        private static async Task<LiveSqlEngineHost> StartMySqlAsync()
        {
            MySqlContainer container = new MySqlBuilder("mysql:8.0")
                .WithDatabase("cachefacade")
                .WithUsername("cachefacade")
                .WithPassword("cachefacade")
                .Build();
            await container.StartAsync().ConfigureAwait(false);
            return CreateHost(SqlDbType.MYSQL, container.GetConnectionString(), container);
        }

        private static async Task<LiveSqlEngineHost> StartMariaDbAsync()
        {
            MariaDbContainer container = new MariaDbBuilder("mariadb:10.11")
                .WithDatabase("cachefacade")
                .WithUsername("cachefacade")
                .WithPassword("cachefacade")
                .Build();
            await container.StartAsync().ConfigureAwait(false);
            return CreateHost(SqlDbType.MARIADB, container.GetConnectionString(), container);
        }

        private static async Task<LiveSqlEngineHost> StartOracleAsync()
        {
            OracleContainer container = new OracleBuilder("gvenzl/oracle-xe:21-slim-faststart")
                .WithPassword("cachefacade")
                .Build();
            await container.StartAsync().ConfigureAwait(false);
            return CreateHost(SqlDbType.ORACLE, container.GetConnectionString(), container);
        }

        private static LiveSqlEngineHost CreateHost(SqlDbType dbType, string connectionString, IContainer container)
        {
            var config = new SqlFacadeConfig(dbType, connectionString);
            ISqlFacade sql = SqlFacadeFactory.GetSqlFacade(config);
            LiveWriteThroughSchema.Ensure(sql);
            return new LiveSqlEngineHost(dbType, sql, container);
        }

        private static bool IsDockerUnavailable(Exception ex)
        {
            for (Exception cur = ex; cur != null; cur = cur.InnerException)
            {
                string msg = cur.Message ?? "";
                string type = cur.GetType().FullName ?? "";
                if (type.Contains("DockerUnavailable", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("Cannot connect to the Docker", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("docker.sock", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("No such file or directory", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("The Docker daemon", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
