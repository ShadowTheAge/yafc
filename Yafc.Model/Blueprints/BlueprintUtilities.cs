using System;
using System.Collections.Generic;
using SDL2;
using Yafc.Model;

namespace Yafc.Blueprints;

public static class BlueprintUtilities {
    private static string ExportBlueprint(BlueprintString blueprint, bool copyToClipboard) {
        string result = blueprint.ToBpString();

        if (copyToClipboard) {
            _ = SDL.SDL_SetClipboardText(result);
        }

        return result;
    }

    public static string ExportConstantCombinators(string name, IReadOnlyList<(Goods item, int amount)> goods, bool copyToClipboard = true) {
        int combinatorCount = ((goods.Count - 1) / Database.constantCombinatorCapacity) + 1;
        int offset = -combinatorCount / 2;
        BlueprintString blueprint = new BlueprintString(name);
        int index = 0;
        BlueprintEntity? last = null;

        for (int i = 0; i < combinatorCount; i++) {
            BlueprintControlBehavior controlBehavior = new BlueprintControlBehavior();
            BlueprintEntity entity = new BlueprintEntity { index = i + 1, position = { x = i + offset, y = 0 }, name = "constant-combinator", controlBehavior = controlBehavior };
            blueprint.blueprint.entities.Add(entity);

            for (int j = 0; j < Database.constantCombinatorCapacity; j++) {
                var (item, amount) = goods[index++];
                BlueprintControlFilter filter = new BlueprintControlFilter { index = j + 1, count = amount };
                filter.signal.Set(item);
                controlBehavior.filters.Add(filter);

                if (index >= goods.Count) {
                    break;
                }
            }

            if (last != null) {
                entity.Connect(last);
            }

            last = entity;
        }

        return ExportBlueprint(blueprint, copyToClipboard);
    }

    public static string ExportRequesterChests(string name, IReadOnlyList<(Item item, int amount)> goods, EntityContainer chest, bool copyToClipboard = true) {
        if (chest.logisticSlotsCount <= 0) {
            throw new NotSupportedException("Chest does not have logistic slots");
        }

        int combinatorCount = ((goods.Count - 1) / chest.logisticSlotsCount) + 1;
        int offset = -chest.size * combinatorCount / 2;
        BlueprintString blueprint = new BlueprintString(name);
        int index = 0;

        for (int i = 0; i < combinatorCount; i++) {
            BlueprintEntity entity = new BlueprintEntity { index = i + 1, position = { x = (i * chest.size) + offset, y = 0 }, name = chest.name };
            blueprint.blueprint.entities.Add(entity);

            for (int j = 0; j < chest.logisticSlotsCount; j++) {
                var (item, amount) = goods[index++];
                BlueprintRequestFilter filter = new BlueprintRequestFilter { index = j + 1, count = amount, name = item.name };
                entity.requestFilters.Add(filter);

                if (index >= goods.Count) {
                    break;
                }
            }
        }

        return ExportBlueprint(blueprint, copyToClipboard);
    }
}
