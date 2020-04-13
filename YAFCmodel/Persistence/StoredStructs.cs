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
            var result = Database.objectsByTypeName.TryGetValue((type, name), out var res) ? res as T : null;
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
        public NodeId id;

        protected void SerializeCommon(NodeConfiguration node)
        {
            x = node.x;
            y = node.y;
            id = node.nodeId;
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
            var data = new RecipeConfiguration(workspace, recipe.Deserialize(validator, true), id);
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
            var data = new BufferConfiguration(workspace, type.Deserialize(validator, true), id) {requestedAmount = requestedAmount};
            Deserialize(data, validator);
            return data;
        }
    }

    [Serializable]
    public struct StoredPortInfo
    {
        public NodeId id;
        public int index;
        public bool input;
    }

    [Serializable]
    public class StoredConnection
    {
        public StoredFactorioObject<Goods> type;
        public StoredPortInfo[] ports = Array.Empty<StoredPortInfo>();

        public StoredConnection() {}
        public StoredConnection(ConnectionConfiguration connection)
        {
            type = connection.type;
            ports = new StoredPortInfo[connection.ports.Count];
            for (var i = 0; i < ports.Length; i++)
            {
                var target = connection.ports[i];
                ports[i].id = target.configuration.nodeId;
                if (target is RecipeIngredient ingr)
                {
                    ports[i].input = true;
                    ports[i].index = Array.IndexOf(target.configuration.ingredients, ingr);
                } else if (target is RecipeProduct prod)
                {
                    ports[i].input = false;
                    ports[i].index = Array.IndexOf(target.configuration.products, prod);
                }
            }
        }

        public void Deserialize(ConnectionConfiguration configuration, IDataValidator validator)
        {
            var type = configuration.type;
            foreach (var port in ports)
            {
                if (!configuration.workspace.GetNodeById(port.id, out var node))
                    validator.ReportError(ValidationErrorSeverity.DataCorruption, "Node not found");
                else if (node != null)
                {
                    var list = port.input ? (NodePort[])node.ingredients : node.products;
                    if (list.Length > port.index && list[port.index].goods == type)
                    {
                        ConnectTo(list[port.index], validator, configuration);
                    }
                    else
                    {
                        var found = false;
                        foreach (var other in list)
                        {
                            if (other.goods == type)
                            {
                                validator.ReportError(ValidationErrorSeverity.MinorDataLoss, "Recipe product order was changed");
                                ConnectTo(other, validator, configuration);
                                found = true;
                                break;
                            }
                        }
                        
                        if (!found)
                            validator.ReportError(ValidationErrorSeverity.MajorDataLoss, "Recipe product was removed");
                    }
                }
            }

            if (configuration.ports.Count < 2)
            {
                validator.ReportError(ValidationErrorSeverity.MinorDataLoss, "Connection connects less than 2 ports, will be removed");
                throw new DeserializationFailedException("Invalid connection");
            }
        }

        public ConnectionConfiguration Deserialize(WorkspaceConfiguration workspace, IDataValidator validator)
        {
            var type = this.type.Deserialize(validator, true);
            var result = new ConnectionConfiguration(workspace, type);
            Deserialize(result, validator);
            return result;
        }

        private void ConnectTo(NodePort port, IDataValidator validator, ConnectionConfiguration result)
        {
            if (port.connection != null)
            {
                validator.ReportError(ValidationErrorSeverity.DataCorruption, "There are multiple connections to one of the ports, only one will remain");
                return;
            }
            result.ports.Add(port);
            port.connection = result;
        }
    }
}