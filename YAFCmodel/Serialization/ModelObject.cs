using System;
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
    public abstract class ModelObject
    {
        private uint _objectVersion;
        private uint _hierarchyVersion;
        public uint objectVersion 
        { 
            get => _objectVersion;
            internal set
            {
                _objectVersion = value;
                hierarchyVersion = value;
            }
        }

        public uint hierarchyVersion
        {
            get => _hierarchyVersion;
            private set
            {
                if (_hierarchyVersion == value)
                    return;
                _hierarchyVersion = value;
                if (owner != null)
                    owner.hierarchyVersion = value;
            }
        }

        protected readonly UndoSystem undo;
        [SkipSerialization] public ModelObject owner { get; protected set; }

        protected ModelObject(ModelObject owner)
        {
            this.owner = owner;
            undo = owner.undo;
            _objectVersion = _hierarchyVersion = undo.version;
        }

        internal ModelObject(UndoSystem undo)
        {
            this.undo = undo;
        }
        
        protected internal virtual void AfterDeserialize() {}
        protected internal virtual void ThisChanged(bool visualOnly) {}
        internal SerializationMap GetUndoBuilder() => SerializationMap.GetSerializationMap(GetType());
        internal void CreateUndoSnapshot(bool visualOnly = false) => undo?.CreateUndoSnapshot(this, visualOnly);
        protected virtual void WriteExtraUndoInformation(UndoSnapshotBuilder builder) {}
        protected virtual void ReadExtraUndoInformation(UndoSnapshotReader reader) {}
    }
}