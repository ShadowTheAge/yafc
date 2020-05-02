using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using YAFC.UI;

namespace YAFC.Model
{
    public class UndoSystem
    {
        private ulong undoVersion;
        private readonly List<UndoSnapshot> currentUndoBatch = new List<UndoSnapshot>();
        private readonly List<Serializable> changedList = new List<Serializable>();
        private readonly Stack<UndoBatch> undo = new Stack<UndoBatch>();
        private readonly Stack<UndoBatch> redo = new Stack<UndoBatch>();
        internal void RecordChange(Serializable target, bool visualOnly)
        {
            if (changedList.Count == 0)
            {
                undoVersion++;
                Ui.ExecuteInMainThread(MakeUndoBatch, this);
            }
            
            if (target.version == undoVersion)
                return;

            changedList.Add(target);
            target.version = undoVersion;
            if (visualOnly)
            {
                var recentUndoBatch = undo.Count == 0 ? default : undo.Peek();
                if (recentUndoBatch.Contains(target))
                    return;
            }

            var builder = target.GetUndoBuilder();
            currentUndoBatch.Add(builder.MakeUndoSnapshot(target));
        }

        private static readonly SendOrPostCallback MakeUndoBatch = delegate(object state)
        {
            var system = state as UndoSystem;
            for (var i = 0; i < system.changedList.Count; i++)
                system.changedList[i].DelayedChanged();
            system.changedList.Clear();
            if (system.currentUndoBatch.Count == 0)
                return;
            var batch = new UndoBatch(system.currentUndoBatch.ToArray());
            system.undo.Push(batch);
            system.redo.Clear();
            system.currentUndoBatch.Clear();
        };

        public void PerformUndo()
        {
            if (undo.Count == 0)
                return;
            redo.Push(undo.Pop().Restore());
        }

        public void PerformRedo()
        {
            if (redo.Count == 0)
                return;
            undo.Push(redo.Pop().Restore());
        }
    }
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

        public UndoSnapshot Restore()
        {
            var builder = target.GetUndoBuilder();
            var redo = builder.MakeUndoSnapshot(target);
            builder.RevertToUndoSnapshot(target, this);
            return redo;
        }
    }

    internal readonly struct UndoBatch
    {
        public readonly UndoSnapshot[] snapshots;
        public UndoBatch(UndoSnapshot[] snapshots)
        {
            this.snapshots = snapshots;
        }
        public UndoBatch Restore()
        {
            for (var i = 0; i < snapshots.Length; i++)
                snapshots[i] = snapshots[i].Restore();
            foreach (var snapshot in snapshots)
                snapshot.target.AfterDeserialize();
            foreach (var snapshot in snapshots)
                snapshot.target.DelayedChanged(); 
            return this;
        }
        
        public bool Contains(Serializable target)
        {
            if (snapshots == null)
                return false;
            foreach (var snapshot in snapshots)
            {
                if (snapshot.target == target)
                    return true;
            }
            return false;
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

    internal struct UndoSnapshotReader
    {
        public readonly BinaryReader reader;
        private int refId;
        private readonly object[] managed;

        public UndoSnapshotReader(UndoSnapshot snapshot)
        {
            if (snapshot.unmanagedData != null)
            {
                var stream = new MemoryStream(snapshot.unmanagedData, false);
                reader = new BinaryReader(stream);
            }
            else reader = null;
            managed = snapshot.managedReferences;
            refId = 0;
        }

        public object ReadManagedReference() => managed[refId++];
    }
}