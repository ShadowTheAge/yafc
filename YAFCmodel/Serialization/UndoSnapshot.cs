using System;
using System.Collections.Generic;
using System.IO;

namespace YAFC.Model
{
    internal readonly struct UndoSnapshot
    {
        internal readonly Serializable target;
        internal readonly object[] managedReferences;
        internal readonly byte[] unmanagedData;

        public UndoSnapshot(Serializable target, object[] managed, byte[] unmanaged)
        {
            this.target = target;
            managedReferences = managed;
            unmanagedData = unmanaged;
        }
    }

    internal class UndoSnapshotBuilder
    {
        private readonly MemoryStream stream = new MemoryStream();
        private readonly List<object> managedRefs = new List<object>();
        public readonly BinaryWriter writer;
        private Serializable currentTarget;

        public UndoSnapshotBuilder()
        {
            writer = new BinaryWriter(stream);
        }

        public void BeginBuilding(Serializable target)
        {
            currentTarget = target;
        }

        public UndoSnapshot Build()
        {
            byte[] buffer = null;
            if (stream.Position > 0)
            {
                buffer = new byte[stream.Position];
                Array.Copy(stream.GetBuffer(), buffer, stream.Position);
            }
            var result = new UndoSnapshot(currentTarget, managedRefs.Count > 0 ? managedRefs.ToArray() : null, buffer);
            stream.Position = 0;
            managedRefs.Clear();
            currentTarget = null;
            return result;
        }

        public void WriteManagedReference(object reference) => managedRefs.Add(reference);
        public void WriteManagedReferences(IEnumerable<object> references) => managedRefs.AddRange(references);
    }

    internal class UndoSnapshotReader
    {
        public readonly BinaryReader reader;
        private int refId;
        private readonly object[] managed;

        public UndoSnapshotReader(UndoSnapshot snapshot)
        {
            var stream = new MemoryStream(snapshot.unmanagedData, false);
            reader = new BinaryReader(stream);
            managed = snapshot.managedReferences;
        }

        public object ReadManagedReference() => managed[refId++];
    }
}