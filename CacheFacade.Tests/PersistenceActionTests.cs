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
            Assert.That(persistence.Id, Is.Not.Null);
            Assert.That(persistence.WriteType, Is.Not.Null);

            // Check Equals method
            Assert.That(persistence,  Is.EqualTo(new PersistenceAction(id, writeType)));

            // Check Equals method
            Assert.That(persistence,  Is.Not.EqualTo(new PersistenceAction("2", writeType)));

            // Check Equals method
            Assert.That(persistence,  Is.Not.EqualTo(new PersistenceAction(id, WriteType.Update)));

            // Check Equals method
            Assert.That(persistence, Is.Not.EqualTo("3"));

            // Check GetHashCode method
            Assert.That(persistence.GetHashCode(),  Is.EqualTo(new PersistenceAction(id, writeType).GetHashCode()));
        }
    }
}
