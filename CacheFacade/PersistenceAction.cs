// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System;

    public class PersistenceAction
    {
        public PersistenceAction(string id, WriteType writeType)
        {
            this.Id = id;
            this.WriteType = writeType;
        }

        public string Id { get; }

        public WriteType WriteType { get; }

        public override string ToString()
        {
            return $"{this.WriteType}:{this.Id}";
        }

        public override bool Equals(object obj)
        {
            PersistenceAction other = obj as PersistenceAction;
            if (other != null)
            {
                return string.Equals(this.Id, other.Id, StringComparison.Ordinal) && this.WriteType == other.WriteType;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return this.ToString().GetHashCode(StringComparison.Ordinal);
        }
    }
}
