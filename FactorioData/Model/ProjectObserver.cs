using System.Collections.Generic;

namespace FactorioData
{
    internal enum UndoType
    {
        Change,
        ChangeVisualOnly,
        Creation,
        Destruction
    }
    
    public class ProjectObserver
    {
        internal static readonly IDataValidator validator = new UndoDataValidator();

        private int undoVersion;
        private readonly List<(Configuration config, object stored)> currentUndoBatch = new List<(Configuration config, object stored)>();
        private readonly List<Configuration> changedList = new List<Configuration>();
        private readonly ProjectConfiguration project;
        private readonly Stack<UndoBatch> undo = new Stack<UndoBatch>();
        private readonly Stack<UndoBatch> redo = new Stack<UndoBatch>();
        
        internal ProjectObserver(ProjectConfiguration project)
        {
            this.project = project;
        }

        internal void Record(Configuration configuration, UndoType type)
        {
            if (!configuration.spawned)
                return;

            if (changedList.Count == 0)
            {
                undoVersion++;
                EnvironmentSettings.dispatcher.DispatchInMainThread(MakeUndoBatch);
            }
            
            if (configuration.version == undoVersion)
                return;

            changedList.Add(configuration);
            configuration.version = undoVersion;
            if (type == UndoType.ChangeVisualOnly)
            {
                var recentUndoBatch = undo.Count == 0 ? default : undo.Peek();
                if (recentUndoBatch.Contains(configuration))
                    return;
            }
            else
            {
                var workspace = configuration.ownerWorkspace;
                if (workspace != null)
                    workspace.modelChangedSinceLastSolveStarted = true;
            }
            currentUndoBatch.Add((configuration, type == UndoType.Creation ? null : configuration.CreateUndoSnapshot()));
        }

        private void MakeUndoBatch()
        {
            for (var i = 0; i < changedList.Count; i++)
                changedList[i].DispatchChangedEvent();
            changedList.Clear();
            if (currentUndoBatch.Count == 0)
                return;
            var batch = new UndoBatch(currentUndoBatch.ToArray());
            undo.Push(batch);
            redo.Clear();
            currentUndoBatch.Clear();
        }

        public void PerformUndo()
        {
            if (undo.Count == 0)
                return;
            redo.Push(undo.Pop().Restore());
        }

        public void PerformRedo()
        {
            if (redo.Count == 0)
                return;
            undo.Push(redo.Pop().Restore());
        }

        private class UndoDataValidator : IDataValidator
        {
            public void ReportError(ValidationErrorSeverity severity, string message)
            {
                throw new DeserializationFailedException(message);
            }
        }
    }

    internal struct UndoBatch
    {
        private readonly (Configuration config, object stored)[] data;

        public UndoBatch((Configuration config, object stored)[] data)
        {
            this.data = data;
        }

        public UndoBatch Restore()
        {
            for (var i = 0; i < data.Length; i++)
            {
                var (config, stored) = data[i];
                object reverted;
                if (config.spawned)
                {
                    reverted = config.CreateUndoSnapshot();
                    if (stored == null)
                    {
                        config.Unspawn();
                        config.spawned = false;
                    }
                    else
                        config.RevertToUndoSnapshot(stored);
                }
                else
                {
                    reverted = null;
                    if (stored != null)
                    {
                        config.RevertToUndoSnapshot(stored);
                        config.Spawn();
                        config.spawned = true;
                    }
                }
                data[i] = (config, reverted);
            }

            foreach (var prop in data)
                prop.config.DispatchChangedEvent();
            
            return this;
        }

        public bool Contains(Configuration configuration)
        {
            if (data == null)
                return false;
            foreach (var (cfg, _) in data)
                if (cfg == configuration)
                    return true;
            return false;
        }
    }
}