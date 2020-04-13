using System;
using System.Collections.Generic;
using YAFC.UI;

namespace YAFC.Model
{
    public struct WorkspaceParameters
    {
        public string name;
        public FactorioObject icon;
    }
    
    public enum WorkspaceId {None = -1}
    public enum NodeId {None = -1}
    
    public class WorkspaceConfiguration : Configuration, IDisposable, IIconAndName
    {
        public ProjectConfiguration project;
        public readonly WorkspaceId id;
        public WorkspaceParameters parameters;
        
        private Dictionary<NodeId, NodeConfiguration> nodes = new Dictionary<NodeId, NodeConfiguration>();
        private List<ConnectionConfiguration> connections = new List<ConnectionConfiguration>();
        public WorkspaceSolver.SolverResult solverResult;
        public ICollection<NodeConfiguration> allNodes => nodes.Values;
        public IReadOnlyList<ConnectionConfiguration> allConnections => connections;

        private WorkspaceSolver solver;
        internal bool modelChangedSinceLastSolveStarted;
        private bool modelSolveInProgress;
        private bool resolveRequested;

        public event Action<ConnectionConfiguration> NewConnection;
        public event Action<NodeConfiguration> NewNode; 

        public WorkspaceConfiguration(ProjectConfiguration project, WorkspaceId id)
        {
            this.project = project;
            this.id = id;
        }

        public NodeId GenerateId()
        {
            NodeId id;
            do
            {
                id = (NodeId)EnvironmentSettings.random.Next();
            } while (nodes.ContainsKey(id));
            return id;
        }

        internal void NewSolutionArrived()
        {
            modelSolveInProgress = false;
            DispatchChangedEvent();
        }

        internal void QueueSolve()
        {
            if (resolveRequested || !modelChangedSinceLastSolveStarted)
                return;
            if (modelSolveInProgress)
            {
                resolveRequested = true;
            }
            else
            {
                modelSolveInProgress = true;
                modelChangedSinceLastSolveStarted = false;
                resolveRequested = false;
                if (solver == null)
                    solver = new WorkspaceSolver(this);
                solver.Solve();
            }
        }

        public void Dispose()
        {
            solver?.Dispose();
        }

        public Icon icon => parameters.icon?.icon ?? Icon.None;
        public string name => parameters.name ?? "Workspace #"+id;
        internal override WorkspaceConfiguration ownerWorkspace => this;

        internal override object CreateUndoSnapshot() => parameters;

        internal override void RevertToUndoSnapshot(object snapshot)
        {
            parameters = (WorkspaceParameters)snapshot;
        }

        internal override void Unspawn()
        {
            project.RemoveWorkspace(this);
        }

        internal override void Spawn()
        {
            project.AddWorkspace(this);
        }

        public bool GetNodeById(NodeId id, out NodeConfiguration node) => nodes.TryGetValue(id, out node);

        internal void AddNode(NodeConfiguration node)
        {
            nodes.Add(node.nodeId, node);
            NewNode?.Invoke(node);
        }

        internal void RemoveNode(NodeConfiguration node)
        {
            if (nodes.TryGetValue(node.nodeId, out var cur) && cur == node)
                nodes.Remove(node.nodeId);
        }

        internal void AddConnection(ConnectionConfiguration connection)
        {
            connections.Add(connection);
            NewConnection?.Invoke(connection);
        }

        internal void RemoveConnection(ConnectionConfiguration connection)
        {
            connections.Remove(connection);
        }
    }
}