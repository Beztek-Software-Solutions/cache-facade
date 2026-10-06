// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests.Live
{
    using System.Collections;
    using Beztek.Facade.Sql;
    using NUnit.Framework;

    /// <summary>
    /// Feeds live ambient WriteThrough tests one fixture per selected SQL engine.
    /// When <c>CACHEFACADE_LIVE_SQL_ENGINES</c> is unset, yields nothing.
    /// </summary>
    public static class LiveSqlEngineFixtureSource
    {
        public static IEnumerable Engines()
        {
            foreach (DbType dbType in LiveSqlEngineSelection.Resolve())
            {
                yield return new TestFixtureData(dbType)
                    .SetArgDisplayNames(dbType.ToString());
            }
        }
    }
}
