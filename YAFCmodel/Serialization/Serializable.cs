using System.Reflection;
using System.Text.Json.Serialization;

namespace YAFC.Model
{
    /*
     * Base class for objects that can be serialized to JSON and that support undo
     * supports ONLY properties of following types:
     * - Serializable (must be read-only)
     * - List<T> (must be read-only) where T is a supported type
     * - FactorioObject (must be read-write)
     * - bool, int, ulong (must be read-write)
     * - enums with backing int (must be read-write)
     * - string (must be read-write)
     *
     * Also supports non-default constructors that write to read-only properties and/or have "owner" as its first parameter
     */
    public abstract class Serializable
    {
        public ulong version { get; internal set; } // Modified through the undo system
        protected readonly UndoSystem undo;
        protected Serializable(UndoSystem undo)
        {
            this.undo = undo;
        }
        protected Serializable(Serializable owner) : this(owner.undo) {}
        
        protected internal virtual void AfterDeserialize() {}
        protected internal virtual void DelayedChanged() {}
        internal UndoBuilder GetUndoBuilder() => UndoBuilder.GetUndoBuilder(GetType());
        public void RecordChanges(bool visualOnly = false) => undo?.RecordChange(this, visualOnly);
        protected virtual void WriteExtraUndoInformation(UndoSnapshotBuilder builder) {}
        protected virtual void ReadExtraUndoInformation(UndoSnapshotReader reader) {}
    }
}