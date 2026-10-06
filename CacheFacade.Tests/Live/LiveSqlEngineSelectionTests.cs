// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests.Live
{
    using System;
    using System.Collections.Generic;
    using Beztek.Facade.Sql;
    using NUnit.Framework;

    [TestFixture]
    public class LiveSqlEngineSelectionTests
    {
        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable(LiveSqlEngineSelection.EnvVar, null);
        }

        [Test]
        public void Resolve_Unset_ReturnsEmpty()
        {
            Environment.SetEnvironmentVariable(LiveSqlEngineSelection.EnvVar, null);
            Assert.That(LiveSqlEngineSelection.Resolve(), Is.Empty);
            Assert.That(LiveSqlEngineSelection.IsConfigured, Is.False);
        }

        [Test]
        public void Resolve_SingleEngine_ReturnsOne()
        {
            Environment.SetEnvironmentVariable(LiveSqlEngineSelection.EnvVar, "postgres");
            Assert.That(LiveSqlEngineSelection.Resolve(), Is.EqualTo(new[] { DbType.POSTGRES }));
        }

        [Test]
        public void Resolve_All_ReturnsEnlistingEngines_WithoutSqlite()
        {
            Environment.SetEnvironmentVariable(LiveSqlEngineSelection.EnvVar, "all");
            IReadOnlyList<DbType> engines = LiveSqlEngineSelection.Resolve();
            Assert.That(engines, Does.Contain(DbType.POSTGRES));
            Assert.That(engines, Does.Contain(DbType.SQLSERVER));
            Assert.That(engines, Does.Contain(DbType.MYSQL));
            Assert.That(engines, Does.Contain(DbType.MARIADB));
            Assert.That(engines, Does.Contain(DbType.ORACLE));
            Assert.That(engines, Does.Not.Contain(DbType.SQLITE));
            Assert.That(engines.Count, Is.EqualTo(5));
        }

        [Test]
        public void Resolve_Sqlite_Throws()
        {
            Environment.SetEnvironmentVariable(LiveSqlEngineSelection.EnvVar, "sqlite");
            Assert.Throws<ArgumentException>(() => LiveSqlEngineSelection.Resolve());
        }

        [Test]
        public void Resolve_UnknownToken_Throws()
        {
            Environment.SetEnvironmentVariable(LiveSqlEngineSelection.EnvVar, "cosmos");
            Assert.Throws<ArgumentException>(() => LiveSqlEngineSelection.Resolve());
        }
    }
}
