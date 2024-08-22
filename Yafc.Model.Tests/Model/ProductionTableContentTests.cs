using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Yafc.Model.Tests.Model;

public class ProductionTableContentTests {
    [Fact]
    public void ChangeFuelEntityModules_ShouldPreserveFixedAmount() {
        Project project = LuaDependentTestHelper.GetProjectForLua();

        ProjectPage page = new ProjectPage(project, typeof(ProductionTable));
        ProductionTable table = (ProductionTable)page.content;
        table.AddRecipe(Database.recipes.all.Single(r => r.name == "recipe"), DataUtils.DeterministicComparer);
        RecipeRow row = table.GetAllRecipes().Single();

        table.modules.beacon = Database.allBeacons.Single();
        table.modules.beaconModule = Database.allModules.Single(m => m.name == "speed-module");
        table.modules.beaconsPerBuilding = 2;
        table.modules.autoFillPayback = MathF.Sqrt(float.MaxValue);

        RunTest(row, testCombinations, (3 * 3 + 3 * 1) * (9 + 2) * 6); // Crafter&fuel * modules * available fixed values

        // Cycle through all crafters (3 burner, 3 electric), fuels (3+1), and internal modules (9 + empty + default), and call assert for each combination.
        // assert will ensure the currently fixed value has not changed by more than 0.01%.
        static void testCombinations(RecipeRow row, ProductionTable table, Action assert) {
            foreach (EntityCrafter crafter in Database.allCrafters) {
                row.entity = crafter;
                foreach (Goods fuel in crafter.energy.fuels) {
                    row.fuel = fuel;
                    foreach (Module module in Database.allModules.Concat([null])) {
                        ModuleTemplateBuilder builder = new();
                        if (module != null) { builder.list.Add((module, 0)); }
                        row.modules = builder.Build(row);
                        table.Solve((ProjectPage)table.owner).Wait();
                        assert();
                    }
                    row.modules = null;
                    table.Solve((ProjectPage)table.owner).Wait();
                    assert();
                }
            }
        }
    }

    [Fact]
    public void ChangeProductionTableModuleConfig_ShouldPreserveFixedAmount() {
        Project project = LuaDependentTestHelper.GetProjectForLua();

        ProjectPage page = new ProjectPage(project, typeof(ProductionTable));
        ProductionTable table = (ProductionTable)page.content;
        table.AddRecipe(Database.recipes.all.Single(r => r.name == "recipe"), DataUtils.DeterministicComparer);
        RecipeRow row = table.GetAllRecipes().Single();

        List<Module> modules = Database.allModules.Where(m => !m.name.Contains("productivity")).ToList();
        EntityBeacon beacon = Database.allBeacons.Single();

        RunTest(row, testCombinations, (3 * 3 + 3 * 1) * 6 * 13 * 32 * 6); // Crafter&fuel * modules * beacon count * payback values * available fixed values

        // Cycle through all crafters (3 burner, 3 electric), fuels (3+1), and beacon modules (6). Also cycle through 0-12 beacons per building and 32 possible payback values.
        // Call assert for each combination. assert will ensure the currently fixed value has not changed by more than 0.01%.
        void testCombinations(RecipeRow row, ProductionTable table, Action assert) {
            foreach (EntityCrafter crafter in Database.allCrafters) {
                row.entity = crafter;
                foreach (Goods fuel in crafter.energy.fuels) {
                    row.fuel = fuel;
                    foreach (Module module in modules) {
                        for (int beaconCount = 0; beaconCount < 13; beaconCount++) {
                            for (float payback = 1; payback < float.MaxValue; payback *= 16) {
                                if (table.GetType().GetProperty("modules").SetMethod is MethodInfo method) {
                                    // Pre-emptive code for if ProductionTable.modules is made writable.
                                    // The ProductionTable.modules setter must notify all relevant recipes if it is added.
                                    method.Invoke(table, [new ModuleFillerParameters(table) {
                                        beacon = beacon,
                                        beaconModule = module,
                                        beaconsPerBuilding = beaconCount,
                                    }]);
                                }
                                else {
                                    table.modules.beacon = beacon;
                                    table.modules.beaconModule = module;
                                    table.modules.beaconsPerBuilding = beaconCount;
                                }
                                table.Solve((ProjectPage)table.owner).Wait();
                                assert();
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Run the preceeding tests for fixed buildings, fuel, ingredients, and products.
    /// </summary>
    /// <param name="row">The row containing the recipe to test with fixed amounts.</param>
    /// <param name="testCombinations">An action that loops through the various combinations of entities, beacons, etc, and calls its third parameter for each combination.</param>
    /// <param name="expectedAssertCalls">The expected number of calls to <paramref name="testCombinations"/>' third parameter. The test will fail if some of the calls are omitted.</param>
    private static void RunTest(RecipeRow row, Action<RecipeRow, ProductionTable, Action> testCombinations, int expectedAssertCalls) {
        ProductionTable table = row.owner;
        int assertCalls = 0;
        // Ensure that building count remains constant when the building count is fixed.
        row.fixedBuildings = 1;
        testCombinations(row, table, () => { Assert.Equal(1, row.fixedBuildings); assertCalls++; });

        // Ensure that the fuel consumption remains constant, except when the entity and fuel change simultaneously.
        row.fixedFuel = true;
        (Goods oldFuel, float fuelAmount, _, _) = row.FuelInformation;
        testCombinations(row, table, testFuel(row, table));
        row.fixedFuel = false;

        // Ensure that ingredient consumption remains constant across all possible changes.
        foreach (RecipeRowIngredient ingredient in row.Ingredients) {
            float fixedAmount = ingredient.Amount;
            row.fixedIngredient = ingredient.Goods;
            testCombinations(row, table, () => { Assert.Equal(fixedAmount, row.Ingredients.Single(i => i.Goods == ingredient.Goods).Amount, fixedAmount * .0001); assertCalls++; });
        }
        row.fixedIngredient = null;

        // Ensure that product production remains constant across all possible changes.
        foreach (RecipeRowProduct product in row.Products) {
            float fixedAmount = product.Amount;
            row.fixedProduct = product.Goods;
            testCombinations(row, table, () => { Assert.Equal(fixedAmount, row.Products.Single(p => p.Goods == product.Goods).Amount, fixedAmount * .0001); assertCalls++; });
        }

        Assert.Equal(expectedAssertCalls, assertCalls);

        // The complicated tests for when the fixed value is expected to reset when fixed fuels are involved.
        Action testFuel(RecipeRow row, ProductionTable table) => () => {
            if (row.entity.energy.fuels.Contains(oldFuel)) {
                Assert.Equal(fuelAmount, row.FuelInformation.Amount, fuelAmount * .0001);
                assertCalls++;
            }
            else {
                Assert.Equal(0, row.FuelInformation.Amount);
                row.fixedBuildings = 1;
                row.fixedFuel = true;
                table.Solve((ProjectPage)table.owner).Wait();
                (oldFuel, fuelAmount, _, _) = row.FuelInformation;
                assertCalls++;
            }
        };
    }
}
