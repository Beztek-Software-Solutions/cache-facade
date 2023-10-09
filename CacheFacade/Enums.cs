// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    /// <summary>
    /// Defines the supported cache provider types.
    /// </summary>
    public enum CacheProviderType
    {
        Redis,
        Hazelcast,
        Ignite,
        LocalMemory
    }

    public enum CacheType
    {
        NonPersistent,
        WriteThrough,
        WriteBehind,
    }

    public enum WriteType
    {
        Create,
        Update,
        Delete
    }

    public enum SerializationType
    {
        Json,
        None
    }
}
