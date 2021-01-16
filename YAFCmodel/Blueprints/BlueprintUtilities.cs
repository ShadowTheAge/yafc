using System;
using System.Collections.Generic;
using SDL2;
using YAFC.Model;
using YAFC.UI;

namespace YAFC.Blueprints
{
    public static class BlueprintUtilities
    {
        private static string ExportBlueprint(BlueprintString blueprint, bool copyToClipboard)
        {
            var result = blueprint.ToBpString();
            if (copyToClipboard)
                SDL.SDL_SetClipboardText(result);
            return result;
        }
        
        public static string ExportConstantCombinators(string name, IReadOnlyList<(Goods item, int amount)> goods, bool copyToClipboard = true)
        {
            var combinatorCount = ((goods.Count - 1) / Database.constantCombinatorCapacity) + 1;
            var offset = -combinatorCount / 2;
            var blueprint = new BlueprintString {blueprint = {label = name}};
            var index = 0;
            BlueprintEntity last = null;
            for (var i = 0; i < combinatorCount; i++)
            {
                var controlBehaviour = new BlueprintControlBehaviour();
                var entity = new BlueprintEntity {index = i + 1, position = {x = i + offset, y = 0}, name = "constant-combinator", controlBehavior = controlBehaviour};
                blueprint.blueprint.entities.Add(entity);
                for (var j = 0; j < Database.constantCombinatorCapacity; j++)
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

            return ExportBlueprint(blueprint, copyToClipboard);
        }

        public static string ExportRequesterChests(string name, IReadOnlyList<(Item item, int amount)> goods, EntityContainer chest, bool copyToClipboard = true)
        {
            if (chest.logisticSlotsCount <= 0)
                throw new NotSupportedException("Chest does not have logistic slots");
            var combinatorCount = ((goods.Count - 1) / chest.logisticSlotsCount) + 1;
            var offset = -chest.size * combinatorCount / 2;
            var blueprint = new BlueprintString {blueprint = {label = name}};
            var index = 0;
            for (var i = 0; i < combinatorCount; i++)
            {
                var entity = new BlueprintEntity {index = i + 1, position = {x = i*chest.size + offset, y = 0}, name = chest.name};
                blueprint.blueprint.entities.Add(entity);
                for (var j = 0; j < chest.logisticSlotsCount; j++)
                {
                    var elem = goods[index++];
                    var filter = new BlueprintRequestFilter {index = j + 1, count = elem.amount, name = elem.item.name};
                    entity.requestFilters.Add(filter);
                    if (index >= goods.Count)
                        break;
                }
            }

            return ExportBlueprint(blueprint, copyToClipboard);
        }
    }
}