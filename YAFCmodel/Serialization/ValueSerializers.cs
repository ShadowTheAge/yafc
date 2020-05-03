using System;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using BindingFlags = System.Reflection.BindingFlags;

namespace YAFC.Model
{
    internal static class ValueSerializer
    {
        public static bool IsValueSerializerSupported(Type type)
        {
            if (type == typeof(int) || type == typeof(float) || type == typeof(bool) || type == typeof(ulong) || type == typeof(string))
                return true;
            if (typeof(FactorioObject).IsAssignableFrom(type))
                return true;
            if (type.IsEnum && type.GetEnumUnderlyingType() == typeof(int))
                return true;
            if (type.IsClass && !typeof(Serializable).IsAssignableFrom(type) && type.GetConstructor(Type.EmptyTypes) != null)
                return true;
            return false;
        }
    }
    internal abstract class ValueSerializer<T>
    {
        public static readonly ValueSerializer<T> Default = CreateValueSerializer();

        private static ValueSerializer<T> CreateValueSerializer()
        {                
            if (typeof(T) == typeof(int))
                return new IntSerializer() as ValueSerializer<T>;
            if (typeof(T) == typeof(float))
                return new FloatSerializer() as ValueSerializer<T>;
            if (typeof(T) == typeof(bool))
                return new BoolSerializer() as ValueSerializer<T>;
            if (typeof(T) == typeof(ulong))
                return new ULongSerializer() as ValueSerializer<T>;
            if (typeof(T) == typeof(string))
                return new StringSerializer() as ValueSerializer<T>;
            if (typeof(FactorioObject).IsAssignableFrom(typeof(T)))
                return Activator.CreateInstance(typeof(FactorioObjectSerializer<>).MakeGenericType(typeof(T))) as ValueSerializer<T>;
            if (typeof(T).IsEnum && typeof(T).GetEnumUnderlyingType() == typeof(int))
                return Activator.CreateInstance(typeof(EnumSerializer<>).MakeGenericType(typeof(T))) as ValueSerializer<T>;
            if (typeof(T).IsClass && !typeof(Serializable).IsAssignableFrom(typeof(T)) && typeof(T).GetConstructor(Type.EmptyTypes) != null)
                return Activator.CreateInstance(typeof(PlainClassesSerializer<>).MakeGenericType(typeof(T))) as ValueSerializer<T>;
            return null;
        }

        public abstract T ReadFromJson(ref Utf8JsonReader reader);
        public abstract void WriteToJson(Utf8JsonWriter writer, T value);
        public abstract T ReadFromUndoSnapshot(UndoSnapshotReader reader);
        public abstract void WriteToUndoSnapshot(UndoSnapshotBuilder writer, T value);
        public virtual bool CanBeNull() => false;
    }

    internal class IntSerializer : ValueSerializer<int>
    {
        public override int ReadFromJson(ref Utf8JsonReader reader) => reader.GetInt32();
        public override void WriteToJson(Utf8JsonWriter writer, int value) => writer.WriteNumberValue(value);
        public override int ReadFromUndoSnapshot(UndoSnapshotReader reader) => reader.reader.ReadInt32();
        public override void WriteToUndoSnapshot(UndoSnapshotBuilder writer, int value) => writer.writer.Write(value);
    }
    
    internal class FloatSerializer : ValueSerializer<float>
    {
        public override float ReadFromJson(ref Utf8JsonReader reader) => reader.GetSingle();
        public override void WriteToJson(Utf8JsonWriter writer, float value) => writer.WriteNumberValue(value);
        public override float ReadFromUndoSnapshot(UndoSnapshotReader reader) => reader.reader.ReadSingle();
        public override void WriteToUndoSnapshot(UndoSnapshotBuilder writer, float value) => writer.writer.Write(value);
    }
    
    internal class BoolSerializer : ValueSerializer<bool>
    {
        public override bool ReadFromJson(ref Utf8JsonReader reader) => reader.GetBoolean();
        public override void WriteToJson(Utf8JsonWriter writer, bool value) => writer.WriteBooleanValue(value);
        public override bool ReadFromUndoSnapshot(UndoSnapshotReader reader) => reader.reader.ReadBoolean();
        public override void WriteToUndoSnapshot(UndoSnapshotBuilder writer, bool value) => writer.writer.Write(value);
    }
    
    internal class ULongSerializer : ValueSerializer<ulong>
    {
        public override ulong ReadFromJson(ref Utf8JsonReader reader) => reader.GetUInt64();
        public override void WriteToJson(Utf8JsonWriter writer, ulong value) => writer.WriteNumberValue(value);
        public override ulong ReadFromUndoSnapshot(UndoSnapshotReader reader) => reader.reader.ReadUInt64();
        public override void WriteToUndoSnapshot(UndoSnapshotBuilder writer, ulong value) => writer.writer.Write(value);
    }
    
    internal class StringSerializer : ValueSerializer<string>
    {
        public override string ReadFromJson(ref Utf8JsonReader reader) => reader.GetString();
        public override void WriteToJson(Utf8JsonWriter writer, string value) => writer.WriteStringValue(value);
        public override string ReadFromUndoSnapshot(UndoSnapshotReader reader) => reader.ReadManagedReference() as string;
        public override void WriteToUndoSnapshot(UndoSnapshotBuilder writer, string value) => writer.WriteManagedReference(value);
        public override bool CanBeNull() => true;
    }
    
    internal class FactorioObjectSerializer<T> : ValueSerializer<T> where T:FactorioObject
    {
        public override T ReadFromJson(ref Utf8JsonReader reader)
        {
            if (!reader.ReadStartArray())
                return null;
            var type = reader.ReadString();
            var name = reader.ReadString();
            return Database.objectsByTypeName.TryGetValue((type, name), out var obj) ? obj as T : null;
        }

        public override void WriteToJson(Utf8JsonWriter writer, T value)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }
            writer.WriteStartArray();
            writer.WriteStringValue(value.type);
            writer.WriteStringValue(value.name);
            writer.WriteEndArray();
        }
        public override T ReadFromUndoSnapshot(UndoSnapshotReader reader) => reader.ReadManagedReference() as T;
        public override void WriteToUndoSnapshot(UndoSnapshotBuilder writer, T value) => writer.WriteManagedReference(value);
        public override bool CanBeNull() => true;
    }

    internal class EnumSerializer<T> : ValueSerializer<T> where T : struct, Enum
    {
        public EnumSerializer()
        {
            if (Unsafe.SizeOf<T>() != 4)
                throw new NotSupportedException("Only int enums are supported");
        }
        
        public override T ReadFromJson(ref Utf8JsonReader reader)
        {
            var val = reader.GetInt32();
            return Unsafe.As<int, T>(ref val);
        }

        public override void WriteToJson(Utf8JsonWriter writer, T value) => writer.WriteNumberValue(Unsafe.As<T, int>(ref value));

        public override T ReadFromUndoSnapshot(UndoSnapshotReader reader)
        {
            var val = reader.reader.ReadInt32();
            return Unsafe.As<int, T>(ref val);
        }

        public override void WriteToUndoSnapshot(UndoSnapshotBuilder writer, T value) => writer.writer.Write(Unsafe.As<T, int>(ref value));
    }

    internal class PlainClassesSerializer<T> : ValueSerializer<T> where T : class
    {
        private static readonly UndoBuilder builder = UndoBuilder.GetUndoBuilder(typeof(T));
        public override T ReadFromJson(ref Utf8JsonReader reader) => SerializationMap<T>.DeserializeFromJson(null, ref reader, null);
        public override void WriteToJson(Utf8JsonWriter writer, T value) => SerializationMap<T>.SerializeToJson(value, writer);

        public override T ReadFromUndoSnapshot(UndoSnapshotReader reader)
        {
            var obj = reader.ReadManagedReference() as T;
            builder.ReadUndo(obj, reader);
            return obj;
        }
        public override void WriteToUndoSnapshot(UndoSnapshotBuilder writer, T value)
        {
            writer.WriteManagedReference(value);
            builder.BuildUndo(value, writer);
        }
    }
}