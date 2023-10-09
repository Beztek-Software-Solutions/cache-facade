// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using System.Collections.Generic;
    using Beztek.Facade.Cache;
    using Beztek.Facade.Sql;

    internal class TestSqlGenerator : ISqlGenerator<TestEtagCacheable>
    {
        public List<ISqlWrite> GetSqlInsert(string id, Tests.TestEtagCacheable t)
        {
            return new List<ISqlWrite>{ new SqlInsert("test_etag_cacheable")
                .WithField(new Field("id", id))
                .WithField(new Field("value", t.Value))
                .WithField(new Field("created_date", t.CreatedDate))
                .WithField(new Field("updated_date", t.UpdatedDate))
                .WithField(new Field("etag", t.Etag)) };
        }

        public SqlSelect GetSqlSelect(string id)
        {
            return new SqlSelect("test_etag_cacheable")
                .WithField(new Field("id", "Id"))
                .WithField(new Field("value", "Value"))
                .WithField(new Field("created_date", "CreatedDate"))
                .WithField(new Field("updated_date", "UpdatedDate"))
                .WithField(new Field("etag", "Etag"))
                .WithWhere(new Filter().WithExpression(new Expression("id", id)));
        }

        public List<ISqlWrite> GetSqlUpdate(string id, Tests.TestEtagCacheable t)
        {
            return new List<ISqlWrite>{ new SqlUpdate("test_etag_cacheable")
                .WithField(new Field("value", t.Value))
                .WithField(new Field("created_date", t.CreatedDate))
                .WithField(new Field("updated_date", t.UpdatedDate))
                .WithField(new Field("etag", t.Etag))
                .WithFilter(new Expression("id", id)) };
        }

        public List<ISqlWrite> GetSqlDelete(string id)
        {
            return new List<ISqlWrite>{ new SqlDelete("test_etag_cacheable")
                .WithFilter(new Expression("id", id)) };
        }
    }
}
