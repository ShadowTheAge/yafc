using System;
using System.Collections.Generic;

namespace YAFC.Model
{
    [Serializable]
    public struct StoredFactorioObject<T> where T : FactorioObject
    {
        public string type;
        public string name;

        public static implicit operator StoredFactorioObject<T>(T obj) => new StoredFactorioObject<T> {type = obj?.type, name = obj?.name};

        public T Deserialize(IDataValidator validator, bool throwOnNull = false)
        {
            var result = Database.objectsByTypeName.TryGetValue(type + "." + name, out var res) ? res as T : null;
            if (result != null)
                return result;
            if (string.IsNullOrEmpty(type))
            {
                if (throwOnNull)
                {
                    validator.ReportError(ValidationErrorSeverity.DataCorruption, "Unexpected null object");
                    throw new DeserializationFailedException("Unexpected null object");
                }
            }
            else
            {
                var severity = throwOnNull ? ValidationErrorSeverity.MajorDataLoss : ValidationErrorSeverity.MinorDataLoss;
                validator.ReportError(severity, "Object no longer exists");
                if (throwOnNull)
                    throw new DeserializationFailedException("Object no longer exists");
            }

            return null;
        }
    }

    [Serializable]
    public struct StoredFactorioObject
    {
        public string type;
        public string name;
        
        public static implicit operator StoredFactorioObject(FactorioObject obj) => new StoredFactorioObject {type = obj?.type, name = obj?.name};
        
        public static StoredFactorioObject[] ConvertList(IReadOnlyCollection<FactorioObject> from)
        {
            var result = new StoredFactorioObject[from.Count];
            var i = 0;
            foreach (var elem in from)
                result[i++] = elem;
            return result;
        }
        
        public StoredFactorioObject<T> ToTyped<T>() where T : FactorioObject => new StoredFactorioObject<T> {name = name, type = type};

        public static void PopulateList<T>(StoredFactorioObject[] source, ICollection<T> target, IDataValidator validator) where T : FactorioObject
        {
            target.Clear();
            foreach (var o in source)
            {
                var obj = o.ToTyped<T>().Deserialize(validator);
                if (obj != null)
                    target.Add(obj);
            }
        }
    }

    public abstract class StoredNode
    {
        public int x, y;

        protected void SerializeCommon(NodeConfiguration node)
        {
            x = node.x;
            y = node.y;
        }

        protected void DeserializeCommon(NodeConfiguration node)
        {
            node.x = x;
            node.y = y;
        }

        public abstract NodeConfiguration Deserialize(WorkspaceConfiguration workspace, IDataValidator validator);
    }

    [Serializable]
    public class StoredRecipe : StoredNode
    {
        public StoredFactorioObject<Recipe> recipe;
        public StoredFactorioObject<Entity> entity;
        public StoredFactorioObject<Goods> fuel;
        
        public StoredRecipe() {}
        public StoredRecipe(RecipeConfiguration data)
        {
            SerializeCommon(data);
            recipe = data.recipe;
            entity = data.entity;
            fuel = data.fuel;
        }

        public void Deserialize(RecipeConfiguration node, IDataValidator validator)
        {
            DeserializeCommon(node);
            node.entity = entity.Deserialize(validator);
            node.fuel = fuel.Deserialize(validator);
        }

        public override NodeConfiguration Deserialize(WorkspaceConfiguration workspace, IDataValidator validator)
        {
            var data = new RecipeConfiguration(workspace, recipe.Deserialize(validator, true));
            Deserialize(data, validator);
            return data;
        }
    }

    [Serializable]
    public class StoredBuffer : StoredNode
    {
        public StoredFactorioObject<Goods> type;
        public float requestedAmount;
        
        public StoredBuffer() {}
        public StoredBuffer(BufferConfiguration data)
        {
            SerializeCommon(data);
            type = data.type;
            requestedAmount = data.requestedAmount;
        }

        public void Deserialize(BufferConfiguration configuration, IDataValidator validator)
        {
            DeserializeCommon(configuration);
        }

        public override NodeConfiguration Deserialize(WorkspaceConfiguration workspace, IDataValidator validator)
        {
            var data = new BufferConfiguration(workspace, type.Deserialize(validator, true)) {requestedAmount = requestedAmount};
            Deserialize(data, validator);
            return data;
        }
    }

    [Serializable]
    public struct StoredPortInfo
    {
        public int index;
        public bool input;
    }
}