// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System.Runtime.Serialization;

    public interface IEtagEntity : ISerializable
    {
        string Etag { get; set; }
    }
}
