// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
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
                .WithField(new Field("etag", t.Etag))
                .WithField(new Field("is_deleted", t.IsDeleted ? 1 : 0)) };
        }

        public SqlSelect GetSqlSelect(string id)
        {
            return new SqlSelect("test_etag_cacheable")
                .WithField(new Field("id", "Id"))
                .WithField(new Field("value", "Value"))
                .WithField(new Field("created_date", "CreatedDate"))
                .WithField(new Field("updated_date", "UpdatedDate"))
                .WithField(new Field("etag", "Etag"))
                .WithField(new Field("is_deleted", "IsDeleted"))
                .WithWhere(new Filter().WithExpression(new Expression("id", id)));
        }

        public List<ISqlWrite> GetSqlUpdate(string id, Tests.TestEtagCacheable t)
        {
            return new List<ISqlWrite>{ new SqlUpdate("test_etag_cacheable")
                .WithField(new Field("value", t.Value))
                .WithField(new Field("created_date", t.CreatedDate))
                .WithField(new Field("updated_date", t.UpdatedDate))
                .WithField(new Field("etag", t.Etag))
                .WithField(new Field("is_deleted", t.IsDeleted ? 1 : 0))
                .WithFilter(new Expression("id", id)) };
        }

        public List<ISqlWrite> GetSqlDelete(string id)
        {
            return new List<ISqlWrite>{ new SqlDelete("test_etag_cacheable")
                .WithFilter(new Expression("id", id)) };
        }

        /// <summary>
        /// Version-gated upsert using sequential etag (cast to integer) and is_deleted.
        /// </summary>
        public List<ISqlWrite> GetSqlUpsert(string id, Tests.TestEtagCacheable t)
        {
            long incomingSequence = EtagUtil.ParseSequentialEtag(t.Etag);

            SqlUpdate update = new SqlUpdate("test_etag_cacheable")
                .WithField(new Field("value", t.Value))
                .WithField(new Field("created_date", t.CreatedDate))
                .WithField(new Field("updated_date", t.UpdatedDate))
                .WithField(new Field("etag", t.Etag))
                .WithField(new Field("is_deleted", t.IsDeleted ? 1 : 0))
                .WithFilter(new Expression("id", id))
                // Parenthesize OR so it does not break out of the id equality predicate.
                .WithFilter(new Expression(
                    $"(etag IS NULL OR CAST(etag AS INTEGER) < {incomingSequence.ToString(CultureInfo.InvariantCulture)})",
                    null).WithIsRaw());

            string cteSql =
                "SELECT " +
                $"{SqlString(id)} AS id, " +
                $"{SqlString(t.Value)} AS value, " +
                $"{SqlDate(t.CreatedDate)} AS created_date, " +
                $"{SqlDate(t.UpdatedDate)} AS updated_date, " +
                $"{SqlString(t.Etag)} AS etag, " +
                $"{(t.IsDeleted ? 1 : 0)} AS is_deleted";

            SqlSelect insertSelect = new SqlSelect("upsert_src")
                .WithField(new Field("id", "id"))
                .WithField(new Field("value", "value"))
                .WithField(new Field("created_date", "created_date"))
                .WithField(new Field("updated_date", "updated_date"))
                .WithField(new Field("etag", "etag"))
                .WithField(new Field("is_deleted", "is_deleted"))
                .WithWhere(new Filter().WithExpression(
                    new Expression("NOT EXISTS (SELECT 1 FROM test_etag_cacheable e WHERE e.id = upsert_src.id)", null).WithIsRaw()));

            SqlInsert insert = new SqlInsert("test_etag_cacheable")
                .WithCommonTableExpression(new CommonTableExpression(cteSql, "upsert_src"))
                .WithQuery(insertSelect);

            return new List<ISqlWrite> { update, insert };
        }

        private static string SqlString(string value)
        {
            if (value == null)
            {
                return "NULL";
            }

            return "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
        }

        private static string SqlDate(DateTime value)
        {
            return "'" + value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "'";
        }
    }
}
