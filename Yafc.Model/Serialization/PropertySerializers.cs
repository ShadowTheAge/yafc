using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Yafc.Model {
    internal enum PropertyType {
        Normal,
        Immutable,
        Obsolete,
        NoUndo,
    }

    internal abstract class PropertySerializer<TOwner> where TOwner : class {
        public readonly PropertyInfo property;
        public readonly JsonEncodedText propertyName;
        internal readonly PropertyType type;

        protected PropertySerializer(PropertyInfo property, PropertyType type, bool usingSetter) {
            this.property = property;
            this.type = type;
            if (property.GetCustomAttribute<ObsoleteAttribute>() != null) {
                this.type = PropertyType.Obsolete;
            }
            else if (property.GetCustomAttribute<NoUndoAttribute>() != null) {
                this.type = PropertyType.NoUndo;
            }
            else if (usingSetter && type == PropertyType.Normal && (!property.CanWrite || property.GetSetMethod() == null)) {
                this.type = PropertyType.Immutable;
            }

            var parameters = property.GetCustomAttribute<JsonPropertyNameAttribute>();
            string name = parameters?.Name ?? property.Name;
            propertyName = JsonEncodedText.Encode(name, JsonUtils.DefaultOptions.Encoder);
        }

        public override string ToString() {
            return typeof(TOwner).Name + "." + property.Name;
        }

        public abstract void SerializeToJson(TOwner owner, Utf8JsonWriter writer);
        public abstract void DeserializeFromJson(TOwner owner, ref Utf8JsonReader reader, DeserializationContext context);
        public abstract void SerializeToUndoBuilder(TOwner owner, UndoSnapshotBuilder builder);
        public abstract void DeserializeFromUndoBuilder(TOwner owner, UndoSnapshotReader reader);
        public virtual object DeserializeFromJson(ref Utf8JsonReader reader, DeserializationContext context) {
            throw new NotSupportedException();
        }

        public virtual bool CanBeNull() {
            return false;
        }
    }

    internal abstract class PropertySerializer<TOwner, TPropertyType>(PropertyInfo property, PropertyType type, bool usingSetter) : PropertySerializer<TOwner>(property, type, usingSetter) where TOwner : class {
        protected readonly Action<TOwner, TPropertyType> setter = property.CanWrite ? property.GetSetMethod()?.CreateDelegate(typeof(Action<TOwner, TPropertyType>)) as Action<TOwner, TPropertyType> : null;
        protected readonly Func<TOwner, TPropertyType> getter = property.CanRead ? property.GetGetMethod().CreateDelegate(typeof(Func<TOwner, TPropertyType>)) as Func<TOwner, TPropertyType> : null;
    }

    internal class ValuePropertySerializer<TOwner, TPropertyType>(PropertyInfo property) : PropertySerializer<TOwner, TPropertyType>(property, PropertyType.Normal, true) where TOwner : class {
        private static readonly ValueSerializer<TPropertyType> ValueSerializer = ValueSerializer<TPropertyType>.Default;

        public override void SerializeToJson(TOwner owner, Utf8JsonWriter writer) {
            ValueSerializer.WriteToJson(writer, getter(owner));
        }

        public override void DeserializeFromJson(TOwner owner, ref Utf8JsonReader reader, DeserializationContext context) {
            setter(owner, ValueSerializer.ReadFromJson(ref reader, context, owner));
        }

        public override void SerializeToUndoBuilder(TOwner owner, UndoSnapshotBuilder builder) {
            ValueSerializer.WriteToUndoSnapshot(builder, getter(owner));
        }

        public override void DeserializeFromUndoBuilder(TOwner owner, UndoSnapshotReader reader) {
            setter(owner, ValueSerializer.ReadFromUndoSnapshot(reader, owner));
        }

        public override object DeserializeFromJson(ref Utf8JsonReader reader, DeserializationContext context) {
            return ValueSerializer.ReadFromJson(ref reader, context, null);
        }

        public override bool CanBeNull() {
            return ValueSerializer.CanBeNull();
        }
    }

    // Serializes read-only sub-value with support of polymorphism
    internal class ReadOnlyReferenceSerializer<TOwner, TPropertyType> : PropertySerializer<TOwner, TPropertyType> where TOwner : ModelObject where TPropertyType : ModelObject {
        public ReadOnlyReferenceSerializer(PropertyInfo property) : base(property, PropertyType.Immutable, false) { }
        public ReadOnlyReferenceSerializer(PropertyInfo property, PropertyType type, bool usingSetter) : base(property, type, usingSetter) { }

        public override void SerializeToJson(TOwner owner, Utf8JsonWriter writer) {
            var instance = getter(owner);
            if (instance == null) {
                writer.WriteNullValue();
            }
            else if (instance.GetType() == typeof(TPropertyType)) {
                SerializationMap<TPropertyType>.SerializeToJson(instance, writer);
            }
            else {
                SerializationMap.GetSerializationMap(instance.GetType()).SerializeToJson(instance, writer);
            }
        }

        public override void DeserializeFromJson(TOwner owner, ref Utf8JsonReader reader, DeserializationContext context) {
            if (reader.TokenType == JsonTokenType.Null) {
                return;
            }

            var instance = getter(owner);
            if (instance.GetType() == typeof(TPropertyType)) {
                SerializationMap<TPropertyType>.PopulateFromJson(getter(owner), ref reader, context);
            }
            else {
                SerializationMap.GetSerializationMap(instance.GetType()).PopulateFromJson(instance, ref reader, context);
            }
        }

        public override void SerializeToUndoBuilder(TOwner owner, UndoSnapshotBuilder builder) { }

        public override void DeserializeFromUndoBuilder(TOwner owner, UndoSnapshotReader reader) { }
    }

    internal class ReadWriteReferenceSerializer<TOwner, TPropertyType>(PropertyInfo property) : ReadOnlyReferenceSerializer<TOwner, TPropertyType>(property, PropertyType.Normal, true)
        where TOwner : ModelObject where TPropertyType : ModelObject {
        public override void DeserializeFromJson(TOwner owner, ref Utf8JsonReader reader, DeserializationContext context) {
            if (reader.TokenType == JsonTokenType.Null) {
                return;
            }

            var instance = getter(owner);
            if (instance == null) {
                setter(owner, SerializationMap<TPropertyType>.DeserializeFromJson(owner, ref reader, context));
                return;
            }

            base.DeserializeFromJson(owner, ref reader, context);
        }

        public override void SerializeToUndoBuilder(TOwner owner, UndoSnapshotBuilder builder) {
            builder.WriteManagedReference(getter(owner));
        }

        public override void DeserializeFromUndoBuilder(TOwner owner, UndoSnapshotReader reader) {
            setter(owner, reader.ReadOwnedReference<TPropertyType>(owner));
        }
    }

    internal class CollectionSerializer<TOwner, TCollection, TElement>(PropertyInfo property) : PropertySerializer<TOwner, TCollection>(property, PropertyType.Normal, false)
        where TCollection : ICollection<TElement> where TOwner : class {
        private static readonly ValueSerializer<TElement> ValueSerializer = ValueSerializer<TElement>.Default;

        public override void SerializeToJson(TOwner owner, Utf8JsonWriter writer) {
            var list = getter(owner);
            writer.WriteStartArray();
            foreach (var elem in list) {
                ValueSerializer.WriteToJson(writer, elem);
            }

            writer.WriteEndArray();
        }

        public override void DeserializeFromJson(TOwner owner, ref Utf8JsonReader reader, DeserializationContext context) {
            var list = getter(owner);
            list.Clear();
            if (reader.ReadStartArray()) {
                while (reader.TokenType != JsonTokenType.EndArray) {
                    var item = ValueSerializer.ReadFromJson(ref reader, context, owner);
                    if (item != null) {
                        list.Add(item);
                    }

                    _ = reader.Read();
                }
            }
        }

        public override void SerializeToUndoBuilder(TOwner owner, UndoSnapshotBuilder builder) {
            var list = getter(owner);
            builder.writer.Write(list.Count);
            foreach (var elem in list) {
                ValueSerializer.WriteToUndoSnapshot(builder, elem);
            }
        }

        public override void DeserializeFromUndoBuilder(TOwner owner, UndoSnapshotReader reader) {
            var list = getter(owner);
            list.Clear();
            int count = reader.reader.ReadInt32();
            for (int i = 0; i < count; i++) {
                list.Add(ValueSerializer.ReadFromUndoSnapshot(reader, owner));
            }
        }
    }

    internal class DictionarySerializer<TOwner, TCollection, TKey, TValue>(PropertyInfo property) : PropertySerializer<TOwner, TCollection>(property, PropertyType.Normal, false)
        where TCollection : IDictionary<TKey, TValue> where TOwner : class {
        private static readonly ValueSerializer<TKey> KeySerializer = ValueSerializer<TKey>.Default;
        private static readonly ValueSerializer<TValue> ValueSerializer = ValueSerializer<TValue>.Default;

        public override void SerializeToJson(TOwner owner, Utf8JsonWriter writer) {
            var list = getter(owner);
            writer.WriteStartObject();
            foreach (var (Key, Value) in list.Select(x => (Key: KeySerializer.GetJsonProperty(x.Key), x.Value)).OrderBy(x => x.Key, StringComparer.Ordinal)) {
                writer.WritePropertyName(Key);
                ValueSerializer.WriteToJson(writer, Value);
            }

            writer.WriteEndObject();
        }

        public override void DeserializeFromJson(TOwner owner, ref Utf8JsonReader reader, DeserializationContext context) {
            var list = getter(owner);
            list.Clear();
            if (reader.ReadStartObject()) {
                while (reader.TokenType != JsonTokenType.EndObject) {
                    var key = KeySerializer.ReadFromJsonProperty(ref reader, context, owner);
                    _ = reader.Read();
                    var value = ValueSerializer.ReadFromJson(ref reader, context, owner);
                    _ = reader.Read();
                    if (key != null && value != null) {
                        list.Add(new KeyValuePair<TKey, TValue>(key, value));
                    }
                }
            }
        }

        public override void SerializeToUndoBuilder(TOwner owner, UndoSnapshotBuilder builder) {
            var list = getter(owner);
            builder.writer.Write(list.Count);
            foreach (var elem in list) {
                KeySerializer.WriteToUndoSnapshot(builder, elem.Key);
                ValueSerializer.WriteToUndoSnapshot(builder, elem.Value);
            }
        }

        public override void DeserializeFromUndoBuilder(TOwner owner, UndoSnapshotReader reader) {
            var list = getter(owner);
            list.Clear();
            int count = reader.reader.ReadInt32();
            for (int i = 0; i < count; i++) {
                list.Add(new KeyValuePair<TKey, TValue>(KeySerializer.ReadFromUndoSnapshot(reader, owner), ValueSerializer.ReadFromUndoSnapshot(reader, owner)));
            }
        }
    }
}
