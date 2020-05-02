using System;

namespace YAFC.Model
{
    public abstract class Configuration
    {
        internal abstract CollectionConfiguration owner { get; }
        internal virtual WorkspaceConfiguration ownerWorkspace => owner.ownerWorkspace;
        internal virtual ProjectConfiguration ownerProject => ownerWorkspace.project;
        internal abstract object CreateUndoSnapshot();
        internal abstract void RevertToUndoSnapshot(object snapshot);
        internal int version { get; set; } // changed through the undo system
        public bool spawned { get; internal set; }

        /*public void Create()
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
        }*/
        
        
        public void RecordUndo(bool visualOnly = false)
        {
            ownerProject.observer.RecordChange(this, visualOnly);
        }

        internal void DispatchChangedEvent()
        {
            changed?.Invoke();
        }

        public event Action changed;
    }

    public abstract class CollectionConfiguration : Configuration
    {
        internal abstract void SpawnChild(Configuration child, object spawnParameters);
        internal abstract object UnspawnChild(Configuration child);
    }
}