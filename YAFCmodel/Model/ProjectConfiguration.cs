using System;
using System.Collections.Generic;

namespace YAFC.Model
{
    public class ProjectConfiguration : CollectionConfiguration
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

        public WorkspaceConfiguration GetWorkspace(WorkspaceId id)
        {
            if (workspaces.TryGetValue(id, out var ws))
                return ws;
            return null;
        }

        internal override CollectionConfiguration owner => null;
        internal override ProjectConfiguration ownerProject => this;
        internal override WorkspaceConfiguration ownerWorkspace => null;

        internal override object CreateUndoSnapshot() => null;

        internal override void RevertToUndoSnapshot(object snapshot) {}

        internal override void SpawnChild(Configuration child, object spawnParameters)
        {
            if (child is WorkspaceConfiguration workspace)
            {
                workspaces.Add(workspace.id, workspace);
                NewWorkspace?.Invoke(workspace);
            }
        }

        internal override object UnspawnChild(Configuration child)
        {
            if (child is WorkspaceConfiguration workspace)
            {
                if (workspaces.TryGetValue(workspace.id, out var prev) && prev == workspace)
                    workspaces.Remove(workspace.id);
            }

            return null;
        }
    }
}