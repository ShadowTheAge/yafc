using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;

namespace YAFC.Model
{
    internal abstract class UndoBuilder
    {
        private static readonly UndoSnapshotBuilder snapshotBuilder = new UndoSnapshotBuilder();

        public UndoSnapshot MakeUndoSnapshot(Serializable target)
        {
            snapshotBuilder.BeginBuilding(target);
            BuildUndo(target, snapshotBuilder);
            return snapshotBuilder.Build();
        }
        public void RevertToUndoSnapshot(Serializable target, UndoSnapshot snapshot)
        {
            ReadUndo(target, new UndoSnapshotReader(snapshot));
        }
        public abstract void BuildUndo(object target, UndoSnapshotBuilder builder);
        public abstract void ReadUndo(object target, UndoSnapshotReader reader);
        
        
        private static readonly Dictionary<Type, UndoBuilder> undoBuilders = new Dictionary<Type, UndoBuilder>();

        public static UndoBuilder GetUndoBuilder(Type type)
        {
            if (undoBuilders.TryGetValue(type, out var builder))
                return builder;
            return undoBuilders[type] = Activator.CreateInstance(typeof(SerializationMap<>.SpecificUndoBuilder).MakeGenericType(type)) as UndoBuilder;
        }
    }
    
    internal static class SerializationMap<T> where T:class
    {
        private static readonly Type parentType;
        private static readonly ConstructorInfo constructor;
        private static readonly PropertySerializer<T>[] properties;
        private static readonly int firstWritableProperty;
        private static readonly ulong requriedConstructorFieldMask;

        public class SpecificUndoBuilder : UndoBuilder
        {
            public override void BuildUndo(object target, UndoSnapshotBuilder builder)
            {
                var t = target as T;
                for (var i = firstWritableProperty; i < properties.Length; i++)
                    properties[i].SerializeToUndoBuilder(t, builder);
            }

            public override void ReadUndo(object target, UndoSnapshotReader reader)
            {
                var t = target as T;
                for (var i = firstWritableProperty; i < properties.Length; i++)
                    properties[i].DeserializeFromUndoBuilder(t, reader);
            }
        }

        static SerializationMap()
        {
            var list = new List<PropertySerializer<T>>();
            
            constructor = typeof(T).GetConstructors(BindingFlags.Public | BindingFlags.Instance)[0];
            var constructorParameters = constructor.GetParameters();
            if (constructorParameters.Length > 0)
            {
                parentType = constructorParameters[0].ParameterType;
                if (!typeof(Serializable).IsAssignableFrom(parentType))
                    throw new NotSupportedException("First parameter of constructor of type "+typeof(T)+" should be 'parent'");
                for (var i = 1; i < constructorParameters.Length; i++)
                {
                    var argument = constructorParameters[i];
                    if (!ValueSerializer.IsValueSerializerSupported(argument.ParameterType))
                        throw new NotSupportedException("Constructor of type "+typeof(T)+" parameter "+argument.Name+" should be value");
                    var property = typeof(T).GetProperty(argument.Name);
                    if (property == null || (property.CanWrite && property.GetSetMethod() != null))
                        throw new NotSupportedException("Constructor of type "+typeof(T)+" parameter "+argument.Name+" should have matching read-only property");
                    var serializer = Activator.CreateInstance(typeof(ValuePropertySerializer<,>).MakeGenericType(typeof(T), argument.ParameterType), property) as PropertySerializer<T>; 
                    list.Add(serializer);
                    if (!serializer.CanBeNull())
                        requriedConstructorFieldMask |= 1ul << (i - 1);
                }
            }

            firstWritableProperty = list.Count;
            
            foreach (var property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var propertyType = property.PropertyType;
                Type serializerType = null;
                if (property.CanWrite && property.GetSetMethod() != null)
                {
                    if (ValueSerializer.IsValueSerializerSupported(propertyType))
                        serializerType = typeof(ValuePropertySerializer<,>);
                    else throw new NotSupportedException("Type "+typeof(T)+" has property "+property.Name+" that cannot be serialized");
                } 
                else
                {
                    if (typeof(Serializable).IsAssignableFrom(propertyType))
                    {
                        serializerType = typeof(ReadOnlyReferenceSerializer<,>);
                    } 
                    else if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(List<>))
                    {
                        propertyType = propertyType.GetGenericArguments()[0];
                        if (ValueSerializer.IsValueSerializerSupported(propertyType))
                            serializerType = typeof(ListOfValuesSerializer<,>);
                        else if (typeof(Serializable).IsAssignableFrom(propertyType))
                            serializerType = typeof(ListOfReferencesSerializer<,>);
                    }
                }

                if (serializerType != null)
                    list.Add(Activator.CreateInstance(serializerType.MakeGenericType(typeof(T), propertyType), property) as PropertySerializer<T>);
            }            
            properties = list.ToArray();
        }

        public static void SerializeToJson(T value, Utf8JsonWriter writer)
        {
            writer.WriteStartObject();
            foreach (var property in properties)
            {
                writer.WritePropertyName(property.propertyName);
                property.SerializeToJson(value, writer);
            }
            writer.WriteEndObject();
        }

        private static PropertySerializer<T> FindProperty(ref Utf8JsonReader reader, ref int lastMatch)
        {
            for (var i = lastMatch+1; i < properties.Length; i++)
            {
                if (reader.ValueTextEquals(properties[i].propertyName.EncodedUtf8Bytes))
                {
                    lastMatch = i;
                    return properties[i];
                }
            }

            for (var i = 0; i < lastMatch; i++)
            {
                if (reader.ValueTextEquals(properties[i].propertyName.EncodedUtf8Bytes))
                {
                    lastMatch = i;
                    return properties[i];
                }
            }

            return null;
        }

        public static T DeserializeFromJson(Serializable owner, ref Utf8JsonReader reader, List<Serializable> allObjects = null)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Expected start object");
            T obj;
            if (parentType != null)
            {
                if (parentType != null && !parentType.IsInstanceOfType(owner))
                    throw new NotSupportedException("Parent is of wrong type");
                var constructorArgs = new object[firstWritableProperty + 1];
                constructorArgs[0] = owner;
                if (firstWritableProperty > 0)
                {
                    var savedReaderState = reader;
                    var lastMatch = -1;
                    reader.Read();
                    var constructorMissingFields = requriedConstructorFieldMask;
                    while (constructorMissingFields != 0 && reader.TokenType == JsonTokenType.PropertyName)
                    {
                        var property = FindProperty(ref reader, ref lastMatch);
                        if (property != null && lastMatch < firstWritableProperty)
                        {
                            reader.Read();
                            constructorMissingFields &= ~(1ul << lastMatch);
                            constructorArgs[lastMatch + 1] = property.DeserializeFromJson(ref reader);
                        }
                        else
                            reader.Skip();
                    }

                    if (constructorMissingFields != 0)
                        throw new JsonException("Json has missing constructor parameters");

                    reader = savedReaderState;
                }

                obj = constructor.Invoke(constructorArgs) as T;
            }
            else
                obj = Activator.CreateInstance<T>();
            var notify = allObjects == null && obj is Serializable;
            if (notify)
                allObjects = new List<Serializable>();
            PopulateFromJson(obj, ref reader, allObjects);
            if (notify)
            {
                foreach (var o in allObjects)
                    o.AfterDeserialize();
                foreach (var o in allObjects)
                    o.DelayedChanged();
            }
            return obj;
        }

        public static void PopulateFromJson(T obj, ref Utf8JsonReader reader, List<Serializable> allObjects)
        {
            if (allObjects != null && obj is Serializable serializable)
                allObjects.Add(serializable);
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Expected start object");
            reader.Read();
            var lastMatch = -1;
            while (reader.TokenType == JsonTokenType.PropertyName)
            {
                var property = FindProperty(ref reader, ref lastMatch);
                if (property == null || lastMatch < firstWritableProperty)
                {
                    if (property == null)
                        Console.Error.WriteLine("Json has extra property: "+reader.GetString());
                    reader.Skip();
                }
                else
                {
                    reader.Read();
                    property.DeserializeFromJson(obj, ref reader, allObjects);
                }
            }
        }
    }
}