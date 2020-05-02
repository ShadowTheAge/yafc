using System.Reflection;
using System.Text.Json.Serialization;

namespace YAFC.Model
{
    /*
     * Base class for objects that can be serialized to JSON and that support undo
     * supports ONLY properties of following types:
     * - Serializable
     * - List<T>
     * - FactorioObject
     * - bool, int, uint, long, ulong
     * - enums
     * - string
     *
     * Also supports non-default constructors that write to read-only properties and/or have "owner" as its first parameter
     */
    public abstract class Serializable
    {
        public abstract object CreateUndoSnapshot();
        public abstract void RevertToUndoSnapshot(object undoSnapshot);
        
        public abstract Serializable owner { get; }
    }
}