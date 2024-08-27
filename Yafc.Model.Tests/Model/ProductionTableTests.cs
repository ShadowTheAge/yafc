using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Yafc.Model.Tests.Model;

[Collection("LuaDependentTests")]
public class ProductionTableTests {
    [Fact]
    public void ProductionTableTest_CanSaveAndLoadWithEmptyPage() {
        Project project = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Model.ProductionTableContentTests.lua");

        ProjectPage page = new(project, typeof(ProductionTable));
        project.pages.Add(page);

        ErrorCollector collector = new();
        using MemoryStream stream = new();
        project.Save(stream);
        Project newProject = Project.Read(stream.ToArray(), collector);

        Assert.Equal(ErrorSeverity.None, collector.severity);
        Assert.Equal(project.pages.Select(s => s.guid), newProject.pages.Select(p => p.guid));
    }

    [Fact]
    public void ProductionTableTest_CanSaveAndLoadWithRecipe() {
        Project project = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Model.ProductionTableContentTests.lua");

        ProjectPage page = new(project, typeof(ProductionTable));
        project.pages.Add(page);
        ProductionTable table = (ProductionTable)page.content;
        table.AddRecipe(Database.recipes.all.Single(r => r.name == "recipe"), DataUtils.DeterministicComparer);

        ErrorCollector collector = new();
        using MemoryStream stream = new();
        project.Save(stream);
        Project newProject = Project.Read(stream.ToArray(), collector);

        Assert.Equal(ErrorSeverity.None, collector.severity);
        Assert.Equal(project.pages.Select(s => s.guid), newProject.pages.Select(p => p.guid));
    }

    [Fact]
    public void ProductionTableTest_CanSaveAndLoadWithEmptySubtable() {
        Project project = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Model.ProductionTableContentTests.lua");

        ProjectPage page = new(project, typeof(ProductionTable));
        project.pages.Add(page);
        ProductionTable table = (ProductionTable)page.content;
        table.AddRecipe(Database.recipes.all.Single(r => r.name == "recipe"), DataUtils.DeterministicComparer);
        RecipeRow row = table.GetAllRecipes().Single();
        row.subgroup = new ProductionTable(row);

        ErrorCollector collector = new();
        using MemoryStream stream = new();
        project.Save(stream);
        Project newProject = Project.Read(stream.ToArray(), collector);

        Assert.Equal(ErrorSeverity.None, collector.severity);
        Assert.Equal(project.pages.Select(s => s.guid), newProject.pages.Select(p => p.guid));
    }

    [Fact]
    public void ProductionTableTest_CanSaveAndLoadWithNonemptySubtable() {
        Project project = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Model.ProductionTableContentTests.lua");

        ProjectPage page = new(project, typeof(ProductionTable));
        project.pages.Add(page);
        ProductionTable table = (ProductionTable)page.content;
        table.AddRecipe(Database.recipes.all.Single(r => r.name == "recipe"), DataUtils.DeterministicComparer);
        RecipeRow row = table.GetAllRecipes().Single();
        row.subgroup = new ProductionTable(row);
        row.subgroup.AddRecipe(Database.recipes.all.Single(r => r.name == "recipe"), DataUtils.DeterministicComparer);

        ErrorCollector collector = new();
        using MemoryStream stream = new();
        project.Save(stream);
        Project newProject = Project.Read(stream.ToArray(), collector);

        Assert.Equal(ErrorSeverity.None, collector.severity);
        Assert.Equal(project.pages.Select(s => s.guid), newProject.pages.Select(p => p.guid));
    }

    [Fact]
    public void ProductionTableTest_CanSaveAndLoadWithProductionSummary() {
        Project project = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Model.ProductionTableContentTests.lua");

        ProjectPage page = new(project, typeof(ProductionSummary));
        project.pages.Add(page);

        ErrorCollector collector = new();
        using MemoryStream stream = new();
        project.Save(stream);
        Project newProject = Project.Read(stream.ToArray(), collector);

        Assert.Equal(ErrorSeverity.None, collector.severity);
        Assert.Equal(project.pages.Select(s => s.guid), newProject.pages.Select(p => p.guid));
    }

    [Fact]
    public void ProductionTableTest_CanSaveAndLoadWithSummary() {
        Project project = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Model.ProductionTableContentTests.lua");

        ProjectPage page = new(project, typeof(Summary));
        project.pages.Add(page);

        ErrorCollector collector = new();
        using MemoryStream stream = new();
        project.Save(stream);
        Project newProject = Project.Read(stream.ToArray(), collector);

        Assert.Equal(ErrorSeverity.None, collector.severity);
        Assert.Equal(project.pages.Select(s => s.guid), newProject.pages.Select(p => p.guid));
    }

    [Fact]
    public void ProductionTableTest_CanLoadWithUnexpectedObject() {
        Project project = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Model.ProductionTableContentTests.lua");

        ProjectPage page = new(project, typeof(ProductionTable));
        project.pages.Add(page);
        ProductionTable table = (ProductionTable)page.content;
        table.AddRecipe(Database.recipes.all.Single(r => r.name == "recipe"), DataUtils.DeterministicComparer);
        RecipeRow row = table.GetAllRecipes().Single();
        row.subgroup = new ProductionTable(row);

        // Force the subgroup to have a value in modules, which is not present in a normal project.
        typeof(ProductionTable).GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .Single(f => f.FieldType == typeof(ModuleFillerParameters))
            .SetValue(row.subgroup, new ModuleFillerParameters(row.subgroup));

        ErrorCollector collector = new();
        using MemoryStream stream = new();
        project.Save(stream);
        Project newProject = Project.Read(stream.ToArray(), collector); // This reader is expected to skip the unexpected value with a MinorDataLoss warning.

        Assert.Equal(ErrorSeverity.MinorDataLoss, collector.severity);
        Assert.Equal(project.pages.Select(s => s.guid), newProject.pages.Select(p => p.guid));
    }
}
