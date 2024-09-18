using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Xunit;
using Yafc.Parser;

namespace Yafc.Model.Tests;

internal static class LuaDependentTestHelper {
    private sealed class Helper : IProgress<(string, string)>, IComparer<FactorioObject> {
        public void Report((string, string) value) { }
        public int Compare(FactorioObject x, FactorioObject y) => x.GetHashCode().CompareTo(y.GetHashCode());
    }

    static LuaDependentTestHelper() {
        Analysis.RegisterAnalysis(Milestones.Instance);
        Analysis.RegisterAnalysis(AutomationAnalysis.Instance);
        Analysis.RegisterAnalysis(TechnologyScienceAnalysis.Instance);
        Analysis.RegisterAnalysis(CostAnalysis.Instance);
        Analysis.RegisterAnalysis(CostAnalysis.InstanceAtMilestones);
    }

    /// <summary>
    /// Runs Sandbox.lua, Serpent.lua, Defines.lua, and a lua file appropriate to the calling test. Returns a <see cref="Project"/> based on those files.
    /// The appropriate lua file is an embedded resource after the calling method, class, and namespace, or the calling class and namespace.
    /// e.g. <c>Yafc.Model.Tests.TestClass.TestMethod1.lua</c> or <c>Yafc.Model.Tests.TestClass.lua</c>.
    /// </summary>
    /// <remarks>For the most convenient usage, ensure the namespace matches the folder structure, and put the files <c>TestClass.TestMethod1.lua</c>,
    /// <c>TestClass.TestMethod2.lua</c>, and <c>TestClass.lua</c> in the same folder as <c>TestClass.cs</c>. Set them as embedded resources.<br/>
    /// <c>TestClass.lua</c> will be used for all tests in TestClass, except TestMethod1 and TestMethod2.<br/>
    /// Do not use <c>require</c> in the embedded files.</remarks>
    /// <param name="targetStreamName">The name of the embedded resource to load, if the default name selection does not work for you.</param>
    internal static Project GetProjectForLua(string targetStreamName = null) {
        // Verify correct non-parallel declaration for tests, to accomodate the singleton analyses.
        StackTrace stack = new();

        for (int i = 1; i < stack.FrameCount; i++) {

            // Search up the stack until we find a method with [Fact] or [Theory].
            MethodBase method = stack.GetFrame(i).GetMethod();
            if (method.GetCustomAttribute<FactAttribute>() != null || method.GetCustomAttribute<TheoryAttribute>() != null) {

                targetStreamName ??= method.DeclaringType.FullName + '.' + method.Name + ".lua";

                // CollectionAttribute doesn't store its constructor argument, so we have to read the attribute data instead of the constructed attribute.
                CustomAttributeData data = method.DeclaringType.GetCustomAttributesData().SingleOrDefault(d => d.AttributeType == typeof(CollectionAttribute), false);
                if ((string)data?.ConstructorArguments[0].Value != "LuaDependentTests") {
                    // Failure to annotate can cause intermittent failures due to parallel execution.
                    // A second test can replace the analysis results while the first is still running.
                    Assert.Fail($"Test classes that call {nameof(LuaDependentTestHelper)}.{nameof(GetProjectForLua)} must be annotated with [Collection(\"LuaDependentTests\")].");
                }

                break;
            }
        }

        if (targetStreamName == null) {
            throw new ArgumentNullException(nameof(targetStreamName), "targetStreamName was not specified and could not be determined by searching the call stack.");
        }

        // Read the four lua files and generate an empty project.
        Project project;
        Helper helper = new();
        using (LuaContext context = new LuaContext()) {
            byte[] bytes = File.ReadAllBytes("Data/Sandbox.lua");
            context.Exec(bytes, "*", "");

            using (Stream stream = GetStream(targetStreamName, out string alternate)) {
                if (stream == null) {
                    Assert.Fail($"""
                        Could not find an embedded resource named "{targetStreamName}" or "{alternate}".
                        If you are not specifying `targetStreamName`, make sure the lua and cs files are in the same folder,
                            the namespace matches the path, and the lua file is either TestClass.lua or TestClass.TestMethod.lua.
                        If you are specifying targetStreamName, be sure you specified the fully-qualified "Yafc.Model.Tests.path.to.filename.lua".
                        In both cases, make sure the file is an <EmbeddedResource> in Yafc.Model.Tests.csproj.
                        """);
                }
                bytes = new byte[stream.Length];
                stream.Read(bytes);
            }
            context.Exec(bytes, "*", "");

            project = new FactorioDataDeserializer(false, new(1, 1)).LoadData(null, context.data, (LuaTable)context.defines["prototypes"]!, false, helper, new(), false);
        }

        DataUtils.SetupForProject(project);
        project.undo.Suspend(); // The undo system relies on SDL.

        return project;
    }

    // Get the stream named either "Yafc.Model.Tests.TestClass.TestMethod.lua" (primary) or "Yafc.Model.Tests.TestClass.lua" (secondary)
    private static Stream GetStream(string targetStreamName, out string alternate) {
        alternate = Path.ChangeExtension(Path.GetFileNameWithoutExtension(targetStreamName), ".lua"); // Remove the method name from the stream name
        return typeof(LuaDependentTestHelper).Assembly.GetManifestResourceStream(targetStreamName)
            ?? typeof(LuaDependentTestHelper).Assembly.GetManifestResourceStream(alternate);
    }
}
