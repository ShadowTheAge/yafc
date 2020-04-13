using System;
using System.Collections.Generic;

namespace FactorioData
{
    public class ProjectConfiguration
    {
        public static readonly Version currentVersion = new Version(0, 1);
        public static readonly Version minCompatibleVersion = new Version(0, 1);
        public readonly ProjectObserver observer;
        
        public readonly Dictionary<WorkspaceId, WorkspaceConfiguration> workspaces = new Dictionary<WorkspaceId, WorkspaceConfiguration>();

        public readonly ProjectSettings settings;

        public ProjectConfiguration()
        {
            observer = new ProjectObserver(this);
            settings = new ProjectSettings(this);
        }
        public event Action<WorkspaceConfiguration> NewWorkspace;

        public WorkspaceId GenerateId()
        {
            WorkspaceId id;
            do
            {
                id = (WorkspaceId)EnvironmentSettings.random.Next();
            } while (workspaces.ContainsKey(id));
            return id;
        }

        internal void AddWorkspace(WorkspaceConfiguration workspace)
        {
            workspaces.Add(workspace.id, workspace);
            NewWorkspace?.Invoke(workspace);
        }

        internal void RemoveWorkspace(WorkspaceConfiguration workspace)
        {
            if (workspaces.TryGetValue(workspace.id, out var prev) && prev == workspace)
                workspaces.Remove(workspace.id);
        }

        public WorkspaceConfiguration GetWorkspace(WorkspaceId id)
        {
            if (workspaces.TryGetValue(id, out var ws))
                return ws;
            return null;
        }
    }
}