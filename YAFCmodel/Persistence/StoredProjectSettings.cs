using System;

namespace YAFC.Model
{
    [Serializable]
    public class StoredProjectSettings
    {
        public WorkspaceId[] tabs;
        public WorkspaceId activeTab;
        public StoredFactorioObject[] milestones;
        public StoredFactorioObject[] objectsMarkedAccessible;
        public StoredFactorioObject[] objectsMarkedInaccessible;

        public StoredProjectSettings() {}

        public StoredProjectSettings(ProjectSettings settings)
        {
            tabs = settings.tabs.ToArray();
            activeTab = settings.activeTab;
            milestones = StoredFactorioObject.ConvertList(settings.milestones);
            objectsMarkedAccessible = StoredFactorioObject.ConvertList(settings.objectsMarkedAccessible);
            objectsMarkedInaccessible = StoredFactorioObject.ConvertList(settings.objectsMarkedInaccessible);
        }

        public void LoadSettings(ProjectSettings settings, IDataValidator validator)
        {
            settings.tabs.Clear();
            settings.tabs.Capacity = tabs.Length;
            var project = settings.project;
            foreach (var tab in tabs)
            {
                if (!project.workspaces.ContainsKey(tab))
                    validator.ReportError(ValidationErrorSeverity.MinorDataLoss, "Tab list references non-existing workspace");
                else settings.tabs.Add(tab);
            }
            
            StoredFactorioObject.PopulateList(milestones, settings.milestones, validator);
            StoredFactorioObject.PopulateList(objectsMarkedAccessible, settings.objectsMarkedAccessible, validator);
            StoredFactorioObject.PopulateList(objectsMarkedInaccessible, settings.objectsMarkedInaccessible, validator);

            if (!project.workspaces.ContainsKey(activeTab))
            {
                validator.ReportError(ValidationErrorSeverity.MinorDataLoss, "Active tab no longer exists");
                settings.activeTab = WorkspaceId.None;
            }

            settings.activeTab = activeTab;
        }
    }
}