using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.OrTools.ConstraintSolver;
using Xunit;

namespace Yafc.Model.Tests.Model;

[Collection("LuaDependentTests")]
public class RecipeParametersTests {
    [Fact]
    public void FluidBoilingRecipes_HaveCorrectConsumption() {
        Project project = LuaDependentTestHelper.GetProjectForLua();

        ProjectPage page = new(project, typeof(ProductionTable));
        project.pages.Add(page);
        ProductionTable table = (ProductionTable)page.content;
        table.AddRecipe(Database.recipes.all.Single(r => r.name == "boiler.boiler.steam"), DataUtils.DeterministicComparer);
        table.AddRecipe(Database.recipes.all.Single(r => r.name == "boiler.heat-exchanger.steam"), DataUtils.DeterministicComparer);

        List<Fluid> water = Database.fluidVariants["Fluid.water"];

        RecipeRow boiler = table.recipes[0];
        RecipeRow heatExchanger = table.recipes[1];
        boiler.fixedBuildings = 1;
        heatExchanger.fixedBuildings = 1;
        table.Solve((ProjectPage)table.owner).Wait(); // Initial Solve to set RecipeRow.Ingredients

        for (int i = 0; i < 3; i++) {
            boiler.ChangeVariant(boiler.Ingredients.Single().Goods, water[i]);
            heatExchanger.ChangeVariant(boiler.Ingredients.Single().Goods, water[i]);

            table.Solve((ProjectPage)table.owner).Wait();

            // boil 60, 78.26, 120 water per second from 15, 50, 90° to 165°
            float expectedBoilerAmount = 1800 / .2f / (165 - water[i].temperature);
            // boil 103.09, 111.11, 121.95 water per second from 15, 50, 90° to 500°
            float expectedHeatExchangerAmount = 10000 / .2f / (500 - water[i].temperature);
            // Equation is boiler power (KW) / heat capacity (KJ/unit°C) / temperature change (°C) => unit/s

            Assert.Equal(.45f, boiler.FuelInformation.Amount, .45f * .0001f); // Always .45 coal per second
            Assert.Equal(expectedBoilerAmount, boiler.Ingredients.Single().Amount, expectedBoilerAmount * .0001f);
            Assert.Equal(expectedBoilerAmount, boiler.Products.Single().Amount, expectedBoilerAmount * .0001f);

            Assert.Equal(10, heatExchanger.FuelInformation.Amount); // Always 10 MW heat
            Assert.Equal(expectedHeatExchangerAmount, heatExchanger.Ingredients.Single().Amount, expectedHeatExchangerAmount * .0001f);
            Assert.Equal(expectedHeatExchangerAmount, heatExchanger.Products.Single().Amount, expectedHeatExchangerAmount * .0001f);
        }
    }
}
