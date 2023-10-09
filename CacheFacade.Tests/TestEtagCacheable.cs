// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using System;
    using System.Runtime.Serialization;

    using Beztek.Facade.Cache;

    [Serializable]
    public class TestEtagCacheable : IEtagEntity
    {
        private const SerializationType SerType = SerializationType.Json;

        public TestEtagCacheable()
        { }

        public TestEtagCacheable(string id, string value, DateTime createdDate, DateTime updatedDate, string etag)
        {
            this.Id = id;
            this.Value = value;
            this.CreatedDate = createdDate;
            this.UpdatedDate = updatedDate;
            this.Etag = etag;
        }

        protected TestEtagCacheable(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            TestEtagCacheable result = SerializationUtil.Deserialize<TestEtagCacheable>(SerType, SerializationUtil.StringToByte(info.GetString("serialized")));
            this.Id = result.Id;
            this.Value = result.Value;
            this.CreatedDate = result.CreatedDate;
            this.UpdatedDate = result.UpdatedDate;
            this.Etag = result.Etag;
        }

        public string Id { get; set; }

        public string Value { get; set; }

        public string Etag { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime UpdatedDate { get; set; }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            info.AddValue("serialized", SerializationUtil.ByteToString(SerializationUtil.Serialize(SerType, this)));
        }

        public override string ToString()
        {
            return SerializationUtil.JsonSerialize(this);
        }

        public override bool Equals(object obj)
        {
            TestEtagCacheable other = obj as TestEtagCacheable;
            if (other != null)
            {
                return string.Equals(this.Id, other.Id, StringComparison.Ordinal)
                    && string.Equals(this.Value.ToString(), other.Value.ToString(), StringComparison.Ordinal)
                    && object.Equals(this.CreatedDate, other.CreatedDate)
                    && object.Equals(this.UpdatedDate, other.UpdatedDate)
                    && string.Equals(this.Etag, other.Etag, StringComparison.Ordinal);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return this.ToString().GetHashCode(StringComparison.Ordinal);
        }
    }
}
