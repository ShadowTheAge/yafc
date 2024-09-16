using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Yafc.Model;
internal static class ValueSerializer {
    public static bool IsValueSerializerSupported(Type type) {
        if (type == typeof(int) || type == typeof(float) || type == typeof(bool) || type == typeof(ulong) || type == typeof(string) 
            || type == typeof(Type) || type == typeof(Guid) || type == typeof(PageReference)) {

            return true;
        }

        if (typeof(FactorioObject).IsAssignableFrom(type)) {
            return true;
        }

        if (type.IsEnum && type.GetEnumUnderlyingType() == typeof(int)) {
            return true;
        }

        if (type.IsClass && (typeof(ModelObject).IsAssignableFrom(type) || type.GetCustomAttribute<SerializableAttribute>() != null)) {
            return true;
        }

        if (!type.IsClass && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>)) {
            return IsValueSerializerSupported(type.GetGenericArguments()[0]);
        }

        return false;
    }
}

internal abstract class ValueSerializer<T> {
    public static readonly ValueSerializer<T> Default = (ValueSerializer<T>)CreateValueSerializer();

    private static object CreateValueSerializer() {
        if (typeof(T) == typeof(int)) {
            return new IntSerializer();
        }

        if (typeof(T) == typeof(float)) {
            return new FloatSerializer();
        }

        if (typeof(T) == typeof(bool)) {
            return new BoolSerializer();
        }

        if (typeof(T) == typeof(ulong)) {
            return new ULongSerializer();
        }

        if (typeof(T) == typeof(string)) {
            return new StringSerializer();
        }

        if (typeof(T) == typeof(Type)) {
            return new TypeSerializer();
        }

        if (typeof(T) == typeof(Guid)) {
            return new GuidSerializer();
        }

        if (typeof(T) == typeof(PageReference)) {
            return new PageReferenceSerializer();
        }

        // null-forgiving: Activator.CreateInstance does not return null.
        if (typeof(FactorioObject).IsAssignableFrom(typeof(T))) {
            return Activator.CreateInstance(typeof(FactorioObjectSerializer<>).MakeGenericType(typeof(T)))!;
        }

        if (typeof(T).IsEnum && typeof(T).GetEnumUnderlyingType() == typeof(int)) {
            return Activator.CreateInstance(typeof(EnumSerializer<>).MakeGenericType(typeof(T)))!;
        }

        if (typeof(T).IsClass) {
            if (typeof(ModelObject).IsAssignableFrom(typeof(T))) {
                return Activator.CreateInstance(typeof(ModelObjectSerializer<>).MakeGenericType(typeof(T)))!;
            }

            return Activator.CreateInstance(typeof(PlainClassesSerializer<>).MakeGenericType(typeof(T)))!;
        }
        if (!typeof(T).IsClass && typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(Nullable<>)) {
            return Activator.CreateInstance(typeof(NullableSerializer<>).MakeGenericType(typeof(T).GetGenericArguments()[0]))!;
        }

        throw new InvalidOperationException($"No known serializer for {typeof(T)}.");
    }

    public abstract T? ReadFromJson(ref Utf8JsonReader reader, DeserializationContext context, object? owner);
    public abstract void WriteToJson(Utf8JsonWriter writer, T? value);
    public virtual string GetJsonProperty(T value) {
        throw new NotSupportedException("Using type " + typeof(T) + " as dictionary key is not supported");
    }

    public virtual T? ReadFromJsonProperty(ref Utf8JsonReader reader, DeserializationContext context, object owner) {
        return ReadFromJson(ref reader, context, owner);
    }

    public abstract T? ReadFromUndoSnapshot(UndoSnapshotReader reader, object owner);
    public abstract void WriteToUndoSnapshot(UndoSnapshotBuilder writer, T? value);
    public virtual bool CanBeNull => false;
}

internal class ModelObjectSerializer<T> : ValueSerializer<T> where T : ModelObject {
    public override T? ReadFromJson(ref Utf8JsonReader reader, DeserializationContext context, object? owner) {
        return SerializationMap<T>.DeserializeFromJson((ModelObject?)owner, ref reader, context);
    }

    public override void WriteToJson(Utf8JsonWriter writer, T? value) {
        SerializationMap<T>.SerializeToJson(value, writer);
    }

    public override T? ReadFromUndoSnapshot(UndoSnapshotReader reader, object owner) {
        return reader.ReadOwnedReference<T>((ModelObject)owner);
    }

    public override void WriteToUndoSnapshot(UndoSnapshotBuilder writer, T? value) {
        writer.WriteManagedReference(value);
    }

    public override bool CanBeNull => true;
}

internal class NullableSerializer<T> : ValueSerializer<T?> where T : struct {
    private static readonly ValueSerializer<T> baseSerializer = ValueSerializer<T>.Default;
    public override T? ReadFromJson(ref Utf8JsonReader reader, DeserializationContext context, object? owner) {
        if (reader.TokenType == JsonTokenType.Null) {
            return null;
        }

        return baseSerializer.ReadFromJson(ref reader, context, owner);
    }

    public override void WriteToJson(Utf8JsonWriter writer, T? value) {
        if (value == null) {
            writer.WriteNullValue();
        }
        else {
            baseSerializer.WriteToJson(writer, value.Value);
        }
    }

    public override T? ReadFromUndoSnapshot(UndoSnapshotReader reader, object owner) {
        if (!reader.reader.ReadBoolean()) {
            return null;
        }

        return baseSerializer.ReadFromUndoSnapshot(reader, owner);
    }

    public override void WriteToUndoSnapshot(UndoSnapshotBuilder writer, T? value) {
        writer.writer.Write(value != null);
        if (value != null) {
            baseSerializer.WriteToUndoSnapshot(writer, value.Value);
        }
    }

    public override bool CanBeNull => true;
}

internal class IntSerializer : ValueSerializer<int> {
    public override int ReadFromJson(ref Utf8JsonReader reader, DeserializationContext context, object? owner) {
        return reader.GetInt32();
    }

    public override void WriteToJson(Utf8JsonWriter writer, int value) {
        writer.WriteNumberValue(value);
    }

    public override int ReadFromUndoSnapshot(UndoSnapshotReader reader, object owner) {
        return reader.reader.ReadInt32();
    }

    public override void WriteToUndoSnapshot(UndoSnapshotBuilder writer, int value) {
        writer.writer.Write(value);
    }
}

internal class FloatSerializer : ValueSerializer<float> {
    public override float ReadFromJson(ref Utf8JsonReader reader, DeserializationContext context, object? owner) {
        return reader.GetSingle();
    }

    public override void WriteToJson(Utf8JsonWriter writer, float value) {
        writer.WriteNumberValue(value);
    }

    public override float ReadFromUndoSnapshot(UndoSnapshotReader reader, object owner) {
        return reader.reader.ReadSingle();
    }

    public override void WriteToUndoSnapshot(UndoSnapshotBuilder writer, float value) {
        writer.writer.Write(value);
    }
}

internal class GuidSerializer : ValueSerializer<Guid> {
    public override Guid ReadFromJson(ref Utf8JsonReader reader, DeserializationContext context, object? owner) {
        return new Guid(reader.GetString()!); // null-forgiving: If reader.GetString() returns null, we don't have a good backup and we'll find out immediately
    }

    public override void WriteToJson(Utf8JsonWriter writer, Guid value) {
        writer.WriteStringValue(value.ToString("N"));
    }

    public override string GetJsonProperty(Guid value) {
        return value.ToString("N");
    }

    public override Guid ReadFromUndoSnapshot(UndoSnapshotReader reader, object owner) {
        return new Guid(reader.reader.ReadBytes(16));
    }

    public override void WriteToUndoSnapshot(UndoSnapshotBuilder writer, Guid value) {
        writer.writer.Write(value.ToByteArray());
    }
}

internal class PageReferenceSerializer : ValueSerializer<PageReference> {
    public override PageReference? ReadFromJson(ref Utf8JsonReader reader, DeserializationContext context, object? owner) {
        string? str = reader.GetString();
        if (str == null) {
            return null;
        }

        return new PageReference(new Guid(str));
    }

    public override void WriteToJson(Utf8JsonWriter writer, PageReference? value) {
        if (value == null) {
            writer.WriteNullValue();
        }
        else {
            writer.WriteStringValue(value.guid.ToString("N"));
        }
    }

    public override PageReference? ReadFromUndoSnapshot(UndoSnapshotReader reader, object owner) {
        return reader.ReadManagedReference() as PageReference;
    }

    public override void WriteToUndoSnapshot(UndoSnapshotBuilder writer, PageReference? value) {
        writer.WriteManagedReference(value);
    }

    public override bool CanBeNull => true;
}

internal class TypeSerializer : ValueSerializer<Type> {
    public override Type? ReadFromJson(ref Utf8JsonReader reader, DeserializationContext context, object? owner) {
        string? s = reader.GetString();

        if (s == null) {
            return null;
        }

        if (s.StartsWith("YAFC.")) {
            s = "Yafc." + s[5..];
        }
        Type? type = Type.GetType(s);
        if (type == null) {
            context.Error("Type " + s + " does not exist. Possible plugin version change", ErrorSeverity.MinorDataLoss);
        }

        return type;
    }
    public override void WriteToJson(Utf8JsonWriter writer, Type? value) {
        ArgumentNullException.ThrowIfNull(value, nameof(value));
        string? name = value.FullName;

        // TODO: Once no one will want to roll back to 0.7.2 or earlier, remove this if block.
        if (name?.StartsWith("Yafc.") ?? false) {
            name = "YAFC." + name[5..];
        }
        writer.WriteStringValue(name);
    }

    public override string GetJsonProperty(Type value) {
        if (value.FullName is null) {
            // If value doesn't have a FullName, we're in a bad state and I don't know what to do.
            throw new ArgumentException($"value must be a type that has a FullName.", nameof(value));
        }

        string name = value.FullName;

        // TODO: Once no one will want to roll back to 0.7.2 or earlier, remove this if block.
        if (name.StartsWith("Yafc.")) {
            name = "YAFC." + name[5..];
        }
        return name;
    }

    public override Type? ReadFromUndoSnapshot(UndoSnapshotReader reader, object owner) {
        return reader.ReadManagedReference() as Type;
    }

    public override void WriteToUndoSnapshot(UndoSnapshotBuilder writer, Type? value) {
        writer.WriteManagedReference(value);
    }
}

internal class BoolSerializer : ValueSerializer<bool> {
    public override bool ReadFromJson(ref Utf8JsonReader reader, DeserializationContext context, object? owner) {
        return reader.GetBoolean();
    }

    public override void WriteToJson(Utf8JsonWriter writer, bool value) {
        writer.WriteBooleanValue(value);
    }

    public override bool ReadFromUndoSnapshot(UndoSnapshotReader reader, object owner) {
        return reader.reader.ReadBoolean();
    }

    public override void WriteToUndoSnapshot(UndoSnapshotBuilder writer, bool value) {
        writer.writer.Write(value);
    }
}

internal class ULongSerializer : ValueSerializer<ulong> {
    public override ulong ReadFromJson(ref Utf8JsonReader reader, DeserializationContext context, object? owner) {
        return reader.GetUInt64();
    }

    public override void WriteToJson(Utf8JsonWriter writer, ulong value) {
        writer.WriteNumberValue(value);
    }

    public override ulong ReadFromUndoSnapshot(UndoSnapshotReader reader, object owner) {
        return reader.reader.ReadUInt64();
    }

    public override void WriteToUndoSnapshot(UndoSnapshotBuilder writer, ulong value) {
        writer.writer.Write(value);
    }
}

internal class StringSerializer : ValueSerializer<string> {
    public override string? ReadFromJson(ref Utf8JsonReader reader, DeserializationContext context, object? owner) {
        return reader.GetString();
    }

    public override void WriteToJson(Utf8JsonWriter writer, string? value) {
        writer.WriteStringValue(value);
    }

    public override string GetJsonProperty(string value) {
        return value;
    }

    public override string? ReadFromUndoSnapshot(UndoSnapshotReader reader, object owner) {
        return reader.ReadManagedReference() as string;
    }

    public override void WriteToUndoSnapshot(UndoSnapshotBuilder writer, string? value) {
        writer.WriteManagedReference(value);
    }

    public override bool CanBeNull => true;
}

internal class FactorioObjectSerializer<T> : ValueSerializer<T> where T : FactorioObject {
    public override T? ReadFromJson(ref Utf8JsonReader reader, DeserializationContext context, object? owner) {
        string? s = reader.GetString();

        if (s == null) {
            return null;
        }

        if (!Database.objectsByTypeName.TryGetValue(s, out var obj)) {
            var substitute = Database.FindClosestVariant(s);

            if (substitute is T t) {
                context.Error("Fluid " + t.locName + " doesn't have correct temperature information. May require adjusting its temperature.", ErrorSeverity.MinorDataLoss);
                return t;
            }

            context.Error("Factorio object '" + s + "' no longer exist. Check mods configuration.", ErrorSeverity.MinorDataLoss);
        }
        return obj as T;
    }

    public override void WriteToJson(Utf8JsonWriter writer, T? value) {
        if (value == null) {
            writer.WriteNullValue();
        }
        else {
            writer.WriteStringValue(value.typeDotName);
        }
    }
    public override string GetJsonProperty(T value) {
        return value.typeDotName;
    }

    public override T? ReadFromUndoSnapshot(UndoSnapshotReader reader, object owner) {
        return reader.ReadManagedReference() as T;
    }

    public override void WriteToUndoSnapshot(UndoSnapshotBuilder writer, T? value) {
        writer.WriteManagedReference(value);
    }

    public override bool CanBeNull => true;
}

internal class EnumSerializer<T> : ValueSerializer<T> where T : struct, Enum {
    public EnumSerializer() {
        if (Unsafe.SizeOf<T>() != 4) {
            throw new NotSupportedException("Only int enums are supported");
        }
    }

    public override T ReadFromJson(ref Utf8JsonReader reader, DeserializationContext context, object? owner) {
        int val = reader.GetInt32();
        return Unsafe.As<int, T>(ref val);
    }

    public override void WriteToJson(Utf8JsonWriter writer, T value) {
        writer.WriteNumberValue(Unsafe.As<T, int>(ref value));
    }

    public override T ReadFromUndoSnapshot(UndoSnapshotReader reader, object owner) {
        int val = reader.reader.ReadInt32();
        return Unsafe.As<int, T>(ref val);
    }

    public override void WriteToUndoSnapshot(UndoSnapshotBuilder writer, T value) {
        writer.writer.Write(Unsafe.As<T, int>(ref value));
    }
}

internal class PlainClassesSerializer<T> : ValueSerializer<T> where T : class {
    private static readonly SerializationMap builder = SerializationMap.GetSerializationMap(typeof(T));
    public override T? ReadFromJson(ref Utf8JsonReader reader, DeserializationContext context, object? owner) {
        return SerializationMap<T>.DeserializeFromJson(null, ref reader, context);
    }

    public override void WriteToJson(Utf8JsonWriter writer, T? value) {
        SerializationMap<T>.SerializeToJson(value, writer);
    }

    public override T ReadFromUndoSnapshot(UndoSnapshotReader reader, object owner) {
        T obj = (T?)reader.ReadManagedReference() ?? throw new InvalidOperationException("Read an unexpected null value from the undo snapshot; cannot undo.");
        builder.ReadUndo(obj, reader);
        return obj;
    }
    public override void WriteToUndoSnapshot(UndoSnapshotBuilder writer, T? value) {
        writer.WriteManagedReference(value ?? throw new InvalidOperationException("Unexpected request to write a null value to the undo snapshot; cannot save undo state."));
        builder.BuildUndo(value, writer);
    }
}
