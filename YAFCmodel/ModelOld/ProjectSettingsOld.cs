using System;
using System.Collections.Generic;

namespace YAFC.Model
{
    public class ProjectSettingsOld : Configuration
    {
        public readonly ProjectConfiguration project;
        internal override CollectionConfiguration owner => project;

        internal ProjectSettingsOld(ProjectConfiguration project)
        {
            this.project = project;
            spawned = true;
        }
        
        internal override WorkspaceConfiguration ownerWorkspace => null;
        internal override ProjectConfiguration ownerProject => project;

        internal override object CreateUndoSnapshot() => new StoredProjectSettings(this);

        internal override void RevertToUndoSnapshot(object snapshot)
        {
            ((StoredProjectSettings)snapshot).LoadSettings(this, ProjectObserver.validator);
        }

        public readonly List<WorkspaceId> tabs = new List<WorkspaceId>();
        public WorkspaceId activeTab;
        public List<FactorioObject> milestones = new List<FactorioObject>();
        public List<FactorioObject> objectsMarkedAccessible = new List<FactorioObject>();
        public List<FactorioObject> objectsMarkedInaccessible = new List<FactorioObject>();
    }
}