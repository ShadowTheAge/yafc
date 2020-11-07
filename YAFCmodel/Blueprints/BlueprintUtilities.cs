using System.Collections.Generic;
using YAFC.Model;
using YAFC.UI;

namespace YAFC.Blueprints
{
    public static class BlueprintUtilities
    {
        public static string ExportConstantCombibators(string name,IReadOnlyList<(Goods item, int amount)> goods)
        {
            const int COMBINATOR_CAPACITY = 18;
            var combinatorCount = ((goods.Count - 1) / COMBINATOR_CAPACITY) + 1;
            var offset = -combinatorCount / 2;
            var blueprint = new BlueprintString();
            blueprint.blueprint.label = name;
            var index = 0;
            BlueprintEntity last = null;
            for (var i = 0; i < combinatorCount; i++)
            {
                var controlBehaviour = new BlueprintControlBehaviour();
                var entity = new BlueprintEntity {entity_number = i + 1, position = {x = i + offset, y = 0}, name = "constant-combinator", control_behavior = controlBehaviour};
                blueprint.blueprint.entities.Add(entity);
                for (var j = 0; j < COMBINATOR_CAPACITY; j++)
                {
                    var elem = goods[index++];
                    var filter = new BlueprintControlFilter {index = j + 1, count = elem.amount};
                    filter.signal.Set(elem.item);
                    controlBehaviour.filters.Add(filter);
                    if (index >= goods.Count)
                        break;
                }
                
                if (last != null)
                    entity.Connect(last);

                last = entity;
            }

            return InputSystem.Instance.control ? blueprint.ToJson() : blueprint.ToBpString();
        }
    }
}