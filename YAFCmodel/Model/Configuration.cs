using System;

namespace YAFC.Model
{
    public abstract class Configuration
    {
        internal abstract WorkspaceConfiguration ownerWorkspace { get; }
        internal virtual ProjectConfiguration ownerProject => ownerWorkspace.project;
        internal abstract object CreateUndoSnapshot();
        internal abstract void RevertToUndoSnapshot(object snapshot);
        internal int version { get; set; } // changed through the undo system
        internal abstract void Unspawn();
        internal abstract void Spawn();
        public bool spawned { get; internal set; }

        public void Create()
        {
            if (spawned)
                return; 
            spawned = true;
            Spawn();
            ownerProject.observer.Record(this, UndoType.Creation);
        }

        public void Destroy()
        {
            if (!spawned)
                return;
            ownerProject.observer.Record(this, UndoType.Destruction);
            spawned = false;
            Unspawn();
        }
        
        
        public void RecordUndo(bool visualOnly = false)
        {
            ownerProject.observer.Record(this, visualOnly ? UndoType.ChangeVisualOnly : UndoType.Change);
        }

        internal void DispatchChangedEvent()
        {
            changed?.Invoke();
        }

        public event Action changed;
    }
}