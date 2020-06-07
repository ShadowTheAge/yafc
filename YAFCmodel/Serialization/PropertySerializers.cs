using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;

namespace YAFC.Model
{
    internal enum PropertyType
    {
        Normal,
        ReadOnly,
        WriteOnly
    }
    internal abstract class PropertySerializer<TOwner>
    {
        public readonly PropertyInfo property;
        public readonly JsonEncodedText propertyName;
        public PropertyType type { get; protected set; }

        protected PropertySerializer(PropertyInfo property)
        {
            this.property = property;
            if (property.GetCustomAttribute<ObsoleteAttribute>() != null)
                type = PropertyType.WriteOnly;
            propertyName = JsonEncodedText.Encode(property.Name, JsonUtils.DefaultOptions.Encoder);
        }

        public override string ToString() => typeof(TOwner).Name + "." + property.Name;

        public abstract void SerializeToJson(TOwner owner, Utf8JsonWriter writer);
        public abstract void DeserializeFromJson(TOwner owner, ref Utf8JsonReader reader, DeserializationContext context);
        public abstract void SerializeToUndoBuilder(TOwner owner, UndoSnapshotBuilder builder);
        public abstract void DeserializeFromUndoBuilder(TOwner owner, UndoSnapshotReader reader);
        public virtual object DeserializeFromJson(ref Utf8JsonReader reader, DeserializationContext context) => throw new NotSupportedException();
        public virtual bool CanBeNull() => false;
    }

    internal abstract class PropertySerializer<TOwner, TPropertyType> : PropertySerializer<TOwner>
    {
        protected readonly Action<TOwner, TPropertyType> setter;
        protected readonly Func<TOwner, TPropertyType> getter;

        protected PropertySerializer(PropertyInfo property) : base(property)
        {
            getter = property.CanRead ? property.GetGetMethod().CreateDelegate(typeof(Func<TOwner, TPropertyType>)) as Func<TOwner, TPropertyType> : null;
            setter = property.CanWrite ? property.GetSetMethod()?.CreateDelegate(typeof(Action<TOwner, TPropertyType>)) as Action<TOwner, TPropertyType> : null;
            if (setter == null)
                type = PropertyType.ReadOnly;
        }
    }

    internal class ValuePropertySerializer<TOwner, TPropertyType> : PropertySerializer<TOwner, TPropertyType>
    {
        private static readonly ValueSerializer<TPropertyType> ValueSerializer = ValueSerializer<TPropertyType>.Default;
        public ValuePropertySerializer(PropertyInfo property) : base(property) {}
        public override void SerializeToJson(TOwner owner, Utf8JsonWriter writer) => ValueSerializer.WriteToJson(writer, getter(owner));
        public override void DeserializeFromJson(TOwner owner, ref Utf8JsonReader reader, DeserializationContext context) => setter(owner, ValueSerializer.ReadFromJson(ref reader, context));
        public override void SerializeToUndoBuilder(TOwner owner, UndoSnapshotBuilder builder) => ValueSerializer.WriteToUndoSnapshot(builder, getter(owner));
        public override void DeserializeFromUndoBuilder(TOwner owner, UndoSnapshotReader reader) => setter(owner, ValueSerializer.ReadFromUndoSnapshot(reader));
        public override object DeserializeFromJson(ref Utf8JsonReader reader, DeserializationContext context) => ValueSerializer.ReadFromJson(ref reader, context);
        public override bool CanBeNull() => ValueSerializer.CanBeNull();
    }

    // Serializes read-only sub-value with support of polymorphism
    internal class ReadOnlyReferenceSerializer<TOwner, TPropertyType> : PropertySerializer<TOwner, TPropertyType> where TOwner:ModelObject where TPropertyType : ModelObject
    {
        public ReadOnlyReferenceSerializer(PropertyInfo property) : base(property) {}

        public override void SerializeToJson(TOwner owner, Utf8JsonWriter writer)
        {
            var instance = getter(owner);
            if (instance == null)
                writer.WriteNullValue();
            else if (instance.GetType() == typeof(TPropertyType))
                SerializationMap<TPropertyType>.SerializeToJson(instance, writer);
            else
                SerializationMap.GetSerializationMap(instance.GetType()).SerializeToJson(instance, writer);
        }

        public override void DeserializeFromJson(TOwner owner, ref Utf8JsonReader reader, DeserializationContext context)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return;
            var instance = getter(owner);
            if (instance.GetType() == typeof(TPropertyType))
                SerializationMap<TPropertyType>.PopulateFromJson(getter(owner), ref reader, context);
            else
                SerializationMap.GetSerializationMap(instance.GetType()).PopulateFromJson(instance, ref reader, context);
        }
        public override void SerializeToUndoBuilder(TOwner owner, UndoSnapshotBuilder builder) {}
        public override void DeserializeFromUndoBuilder(TOwner owner, UndoSnapshotReader reader) {}
    }
    
    internal class ReadWriteReferenceSerializer<TOwner, TPropertyType> : ReadOnlyReferenceSerializer<TOwner, TPropertyType> where TOwner:ModelObject where TPropertyType : ModelObject
    {
        public ReadWriteReferenceSerializer(PropertyInfo property) : base(property) {}
        public override void DeserializeFromJson(TOwner owner, ref Utf8JsonReader reader, DeserializationContext context)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return;
            var instance = getter(owner);
            if (instance == null)
            {
                setter(owner, SerializationMap<TPropertyType>.DeserializeFromJson(owner, ref reader, context));
                return;
            }
            base.DeserializeFromJson(owner, ref reader, context);
        }

        public override void SerializeToUndoBuilder(TOwner owner, UndoSnapshotBuilder builder)
        {
            builder.WriteManagedReference(getter(owner));
        }

        public override void DeserializeFromUndoBuilder(TOwner owner, UndoSnapshotReader reader)
        {
            setter(owner, reader.ReadOwnedReference<TPropertyType>(owner));
        }
    }

    internal class CollectionOfValuesSerializer<TOwner, TCollection, TElement> : PropertySerializer<TOwner, TCollection> where TCollection:ICollection<TElement>
    {
        private static readonly ValueSerializer<TElement> ValueSerializer = ValueSerializer<TElement>.Default;
        public CollectionOfValuesSerializer(PropertyInfo property) : base(property) {}

        public override void SerializeToJson(TOwner owner, Utf8JsonWriter writer)
        {
            var list = getter(owner);
            writer.WriteStartArray();
            foreach (var elem in list)
                ValueSerializer.WriteToJson(writer, elem);
            writer.WriteEndArray();
        }

        public override void DeserializeFromJson(TOwner owner, ref Utf8JsonReader reader, DeserializationContext context)
        {
            var list = getter(owner);
            list.Clear();
            if (reader.ReadStartArray())
            {
                while (reader.TokenType != JsonTokenType.EndArray)
                {
                    var item = ValueSerializer.ReadFromJson(ref reader, context);
                    if (item != null)
                        list.Add(item);
                    reader.Read();
                }
            }
        }

        public override void SerializeToUndoBuilder(TOwner owner, UndoSnapshotBuilder builder)
        {
            var list = getter(owner);
            builder.writer.Write(list.Count);
            foreach (var elem in list)
                ValueSerializer.WriteToUndoSnapshot(builder, elem);
        }

        public override void DeserializeFromUndoBuilder(TOwner owner, UndoSnapshotReader reader)
        {
            var list = getter(owner);
            list.Clear();
            var count = reader.reader.ReadInt32();
            for (var i = 0; i < count; i++)
                list.Add(ValueSerializer.ReadFromUndoSnapshot(reader));
        }
    }
    
    internal class ListOfReferencesSerializer<TOwner, TCollection, TElement> : PropertySerializer<TOwner, TCollection> where TElement : ModelObject where TOwner:ModelObject where TCollection:ICollection<TElement>
    {
        public ListOfReferencesSerializer(PropertyInfo property) : base(property) {}

        public override void SerializeToJson(TOwner owner, Utf8JsonWriter writer)
        {
            var list = getter(owner);
            writer.WriteStartArray();
            foreach (var elem in list)
                SerializationMap<TElement>.SerializeToJson(elem, writer);
            writer.WriteEndArray();
        }

        public override void DeserializeFromJson(TOwner owner, ref Utf8JsonReader reader, DeserializationContext context)
        {
            var list = getter(owner);
            list.Clear();
            if (reader.ReadStartArray())
            {
                while (reader.TokenType != JsonTokenType.EndArray)
                {
                    var elem = SerializationMap<TElement>.DeserializeFromJson(owner, ref reader, context);
                    if (elem != null)
                        list.Add(elem);
                    reader.Read();
                }
            }
        }

        public override void SerializeToUndoBuilder(TOwner owner, UndoSnapshotBuilder builder)
        {
            var list = getter(owner);
            builder.writer.Write(list.Count);
            builder.WriteManagedReferences(list);
        }

        public override void DeserializeFromUndoBuilder(TOwner owner, UndoSnapshotReader reader)
        {
            var list = getter(owner);
            list.Clear();
            var count = reader.reader.ReadInt32();
            for (var i = 0; i < count; i++)
                list.Add(reader.ReadOwnedReference<TElement>(owner));
        }
    }
    
    internal class DictionaryOfValuesSerializer<TOwner, TCollection, TKey, TValue> : PropertySerializer<TOwner, TCollection> where TCollection:IDictionary<TKey, TValue>
    {
        private static readonly ValueSerializer<TKey> KeySerializer = ValueSerializer<TKey>.Default;
        private static readonly ValueSerializer<TValue> ValueSerializer = ValueSerializer<TValue>.Default;
        public DictionaryOfValuesSerializer(PropertyInfo property) : base(property) {}

        public override void SerializeToJson(TOwner owner, Utf8JsonWriter writer)
        {
            var list = getter(owner);
            writer.WriteStartObject();
            foreach (var elem in list)
            {
                KeySerializer.WriteToJsonProperty(writer, elem.Key);
                ValueSerializer.WriteToJson(writer, elem.Value);
            }
            writer.WriteEndObject();
        }

        public override void DeserializeFromJson(TOwner owner, ref Utf8JsonReader reader, DeserializationContext context)
        {
            var list = getter(owner);
            list.Clear();
            if (reader.ReadStartObject())
            {
                while (reader.TokenType != JsonTokenType.EndObject)
                {
                    var key = KeySerializer.ReadFromJsonProperty(ref reader, context);
                    reader.Read();
                    var value = ValueSerializer.ReadFromJson(ref reader, context);
                    reader.Read();
                    if (key != null && value != null)
                        list.Add(new KeyValuePair<TKey, TValue>(key, value));
                }
            }
        }

        public override void SerializeToUndoBuilder(TOwner owner, UndoSnapshotBuilder builder)
        {
            var list = getter(owner);
            builder.writer.Write(list.Count);
            foreach (var elem in list)
            {
                KeySerializer.WriteToUndoSnapshot(builder, elem.Key);
                ValueSerializer.WriteToUndoSnapshot(builder, elem.Value);
            }
        }

        public override void DeserializeFromUndoBuilder(TOwner owner, UndoSnapshotReader reader)
        {
            var list = getter(owner);
            list.Clear();
            var count = reader.reader.ReadInt32();
            for (var i = 0; i < count; i++)
                list.Add(new KeyValuePair<TKey, TValue>(KeySerializer.ReadFromUndoSnapshot(reader), ValueSerializer.ReadFromUndoSnapshot(reader)));
        }
    }
}