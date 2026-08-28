// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{

    /// <summary>
    /// Optimistic-concurrency token for cached entities.
    /// <para>
    /// Library-generated etags are always a <b>short sequential string</b> (UTC epoch milliseconds via
    /// <see cref="EtagUtil.GenerateEtag"/>)—the same format for write-through and write-behind so
    /// switching cache modes does not change etag shape.
    /// </para>
    /// <para>
    /// For write-behind ordering under soft delete, also implement <see cref="IWriteBehindEntity"/>.
    /// See README section "Entity types: recommendations, fallbacks, and compromises".
    /// </para>
    /// </summary>
    public interface IEtagEntity
    {
        string Etag { get; set; }
    }
}
