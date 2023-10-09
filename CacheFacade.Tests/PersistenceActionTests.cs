// Copyright (c) Beztek Software Solutions. All rights reserved.

using NUnit.Framework;

namespace Beztek.Facade.Cache.Tests
{
    /// <summary>
    /// It is important to test the hash code and equals in this class, as it is used as a key in a Dictionary
    /// </summary>
    [TestFixture]
    public class PersistenceActionTests
    {
        [Test]
        public void GetPersistenceAction()
        {
            string id = "1";
            WriteType writeType = WriteType.Create;
            PersistenceAction persistence = new PersistenceAction(id, writeType);
            Assert.IsNotNull(persistence.Id);
            Assert.IsNotNull(persistence.WriteType);

            // Check Equals method
            Assert.AreEqual(persistence, new PersistenceAction(id, writeType));

            // Check Equals method
            Assert.AreNotEqual(persistence, new PersistenceAction("2", writeType));

            // Check Equals method
            Assert.AreNotEqual(persistence, new PersistenceAction(id, WriteType.Update));

            // Check Equals method
            Assert.AreNotEqual(persistence, "3");

            // Check GetHashCode method
            Assert.AreEqual(persistence.GetHashCode(), new PersistenceAction(id, writeType).GetHashCode());
        }
    }
}
