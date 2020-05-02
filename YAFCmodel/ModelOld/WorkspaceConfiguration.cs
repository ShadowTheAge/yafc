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

    public class WorkspaceConfiguration : GroupConfiguration, IDisposable, IIconAndName
    {
        public ProjectConfiguration project;
        public readonly WorkspaceId id;
        public WorkspaceParameters parameters;
        
        public WorkspaceSolver.SolverResult solverResult;
        private WorkspaceSolver solver;
        internal bool modelChangedSinceLastSolveStarted;
        private bool modelSolveInProgress;
        private bool resolveRequested;
        
        public WorkspaceConfiguration(ProjectConfiguration project, WorkspaceId id) : base(null)
        {
            this.project = project;
            this.id = id;
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
    }
}