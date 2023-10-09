// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using System;
    using System.Runtime.Serialization;

    using Beztek.Facade.Cache;

    [Serializable]
    public class TestCacheable : ISerializable
    {
        private const SerializationType SerType = SerializationType.Json;

        public TestCacheable()
        { }

        internal TestCacheable(string id, string value)
        {
            this.Id = id;
            this.Value = value;
        }

        protected TestCacheable(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            TestCacheable result = SerializationUtil.Deserialize<TestCacheable>(SerType, SerializationUtil.StringToByte(info.GetString("serialized")));
            this.Id = result.Id;
            this.Value = result.Value;
        }

        public string Id { get; set; }

        public string Value { get; set; }

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
            TestCacheable other = obj as TestCacheable;
            if (other != null)
            {
                return string.Equals(this.Id, other.Id, StringComparison.Ordinal) && string.Equals(this.Value.ToString(), other.Value.ToString(), StringComparison.Ordinal);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return this.ToString().GetHashCode(StringComparison.Ordinal);
        }
    }
}
