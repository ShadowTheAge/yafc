using System;

namespace Yafc.Model;

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

public abstract class ModelObject {
    internal readonly UndoSystem undo;

    internal ModelObject(UndoSystem undo) {
        this.undo = undo;
        _objectVersion = _hierarchyVersion = undo?.version ?? 0;
    }

    [SkipSerialization] public abstract ModelObject? ownerObject { get; internal set; }
    public ModelObject GetRoot() => ownerObject?.GetRoot() ?? this;

    private uint _objectVersion;
    private uint _hierarchyVersion;
    public uint objectVersion {
        get => _objectVersion;
        internal set {
            _objectVersion = value;
            hierarchyVersion = value;
        }
    }

    public uint hierarchyVersion {
        get => _hierarchyVersion;
        private set {
            if (_hierarchyVersion == value) {
                return;
            }

            _hierarchyVersion = value;
            var owner = ownerObject;
            if (owner != null) {
                owner.hierarchyVersion = value;
            }
        }
    }

    protected internal virtual void AfterDeserialize() { }
    protected internal virtual void ThisChanged(bool visualOnly) => ownerObject?.ThisChanged(visualOnly);

    internal SerializationMap GetUndoBuilder() => SerializationMap.GetSerializationMap(GetType());

    internal void CreateUndoSnapshot(bool visualOnly = false) => undo?.CreateUndoSnapshot(this, visualOnly);

    private protected virtual void WriteExtraUndoInformation(UndoSnapshotBuilder builder) { }
    private protected virtual void ReadExtraUndoInformation(UndoSnapshotReader reader) { }
    public bool justChanged => undo.HasChangesPending(this);
}
public abstract class ModelObject<TOwner>(TOwner owner) : ModelObject(owner.undo) where TOwner : ModelObject {
    private TOwner _owner = owner ?? throw new ArgumentNullException(nameof(owner));

    [SkipSerialization]
    public TOwner owner {
        get => _owner;
        protected set => _owner = value ?? throw new ArgumentNullException(nameof(value));
    }

    public override ModelObject? ownerObject {
        get => owner;
        internal set => owner = value is null ? throw new ArgumentNullException(nameof(value))
                : value as TOwner ?? throw new ArgumentException($"value must be of type {typeof(TOwner)}", nameof(value));
    }
}
