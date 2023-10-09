// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using System;
    using Beztek.Facade.Cache;
    using NUnit.Framework;

    [TestFixture]
    public class SerializationUtilTests
    {
        [Test]
        public void TestJsonSerialization()
        {
            TestSerialization(SerializationType.Json);
        }

        [Test]
        public void TestNonexistentSerialization()
        {
            Assert.Throws<NotSupportedException>(() => TestSerialization(SerializationType.None));
        }

        [Test]
        public void TestBytification()
        {
            string serializedString = SerializationUtil.JsonSerialize(new TestCacheable("test-key", "get-result"));
            byte[] bytifiedString = SerializationUtil.StringToByte(serializedString);
            string stringifiedByte = SerializationUtil.ByteToString(bytifiedString);
            Assert.AreEqual(serializedString, stringifiedByte);
        }

        // Utility method

        private static void TestSerialization(SerializationType serializationType)
        {
            TestCacheable data = new TestCacheable("test-key", "get-result");
            byte[] serialized = SerializationUtil.Serialize(serializationType, data);
            TestCacheable result = SerializationUtil.Deserialize<TestCacheable>(serializationType, serialized);
        }
    }
}
