using System;
using System.Collections.Generic;
using System.Linq;

namespace FactorioData
{
    [Serializable]
    public class StoredWorkspace
    {
        public WorkspaceId id;
        public string name;
        public StoredFactorioObject<FactorioObject> icon;
        public List<StoredRecipe> recipes;
        public List<StoredBuffer> buffers;
        public StoredConnection[] connections;
        public StoredWorkspace() {}

        public StoredWorkspace(WorkspaceConfiguration workspace)
        {
            id = workspace.id;
            name = workspace.parameters.name;
            icon = workspace.parameters.icon;
            recipes = new List<StoredRecipe>();
            buffers = new List<StoredBuffer>();

            foreach (var node in workspace.allNodes.OrderBy(x => x.nodeId))
            {
                switch (node)
                {
                    case RecipeConfiguration recipe:
                        recipes.Add(new StoredRecipe(recipe));
                        break;
                    case BufferConfiguration buffer:
                        buffers.Add(new StoredBuffer(buffer));
                        break;
                }
            }

            var workspaceConnections = workspace.allConnections;
            connections = new StoredConnection[workspaceConnections.Count];
            for (var i = 0; i < workspaceConnections.Count; i++)
                connections[i] = new StoredConnection(workspaceConnections[i]);
        }

        private void DeserializeNode(WorkspaceConfiguration workspace, StoredNode node, IDataValidator validator)
        {
            try
            {
                var result = node.Deserialize(workspace, validator);
                if (workspace.GetNodeById(node.id, out _))
                    validator.ReportError(ValidationErrorSeverity.DataCorruption, "Multiple nodes with same ID");
                workspace.AddNode(result);
            }
            catch (Exception ex)
            {
                if (!(ex is DeserializationFailedException))
                    validator.ReportError(ValidationErrorSeverity.DataCorruption, "Exception: "+ex.Message);
            }
        }

        public WorkspaceConfiguration Load(ProjectConfiguration project, IDataValidator validator)
        {
            var configuration = new WorkspaceConfiguration(project, id)
            {
                parameters = {name = name, icon = icon.Deserialize(validator)},
            };
            
            foreach (var recipe in recipes)
                DeserializeNode(configuration, recipe, validator);
            foreach (var buffer in buffers)
                DeserializeNode(configuration, buffer, validator);

            foreach (var connection in connections)
            {
                try
                {
                    configuration.AddConnection(connection.Deserialize(configuration, validator));
                }
                catch (Exception ex)
                {
                    if (!(ex is DeserializationFailedException))
                        validator.ReportError(ValidationErrorSeverity.DataCorruption, "Exception: "+ex.Message);
                }
            }

            return configuration;
        }
    }
    
    [Serializable]
    public class StoredProject
    {
        public string version;
        public string minVersion;
        public List<StoredWorkspace> workspaces;
        public StoredProjectSettings settings;
        public string[] mods;

        public StoredProject(ProjectConfiguration project)
        {
            version = ProjectConfiguration.currentVersion.ToString();
            minVersion = ProjectConfiguration.minCompatibleVersion.ToString();
            workspaces = new List<StoredWorkspace>(project.workspaces.Count);
            settings = new StoredProjectSettings(project.settings);
            foreach (var workspace in project.workspaces.Values.OrderBy(x => x.id))
                workspaces.Add(new StoredWorkspace(workspace));
            mods = EnvironmentSettings.allMods.OrderBy(x => x).ToArray();
        }

        public ProjectConfiguration LoadProject(IDataValidator validator)
        {
            if (version == null)
            {
                validator.ReportError(ValidationErrorSeverity.CriticalIncompatibility, "This is not a valid YAFC project");
                return null;
            }

            var v = new Version(version);
            if (v != ProjectConfiguration.currentVersion)
            {
                if (v < ProjectConfiguration.currentVersion)
                {
                    validator.ReportError(ValidationErrorSeverity.Hint, "This project was created with older YAFC version "+version+" and will be converted");
                }
                else
                {
                    var minV = new Version(minVersion);
                    if (minV > ProjectConfiguration.currentVersion)
                    {
                        validator.ReportError(ValidationErrorSeverity.CriticalIncompatibility, "This project was created with uncompativle YAFC version "+version+" and cannot be loaded");
                        return null;
                    }
                    validator.ReportError(ValidationErrorSeverity.MajorDataLoss, "This project was created with newer YAFC version "+version+" and newer data may be lost");
                }
            }

            foreach (var mod in mods)
            {
                if (!EnvironmentSettings.allMods.Contains(mod))
                    validator.ReportError(ValidationErrorSeverity.Hint, "Added mod "+mod);
            }

            foreach (var mod in EnvironmentSettings.allMods)
            {
                if (!mods.Contains(mod))
                    validator.ReportError(ValidationErrorSeverity.MinorDataLoss, "Removed mod "+mod);
            }
            
            var project = new ProjectConfiguration();
            foreach (var workspace in workspaces)
            {
                try
                {
                    if (project.workspaces.ContainsKey(workspace.id))
                    {
                        validator.ReportError(ValidationErrorSeverity.DataCorruption, "Multiple workspaces with same id");
                        continue;
                    }
                    project.workspaces[workspace.id] = workspace.Load(project, validator);
                }
                catch (Exception ex)
                {
                    if (!(ex is DeserializationFailedException))
                        validator.ReportError(ValidationErrorSeverity.DataCorruption, "Exception: "+ex.Message);
                }
            }

            if (project.workspaces.Count == 0)
            {
                validator.ReportError(ValidationErrorSeverity.CriticalIncompatibility, "The project is empty");
                return null;
            }
            
            settings.LoadSettings(project.settings, validator);

            return project;
        }
    }
}