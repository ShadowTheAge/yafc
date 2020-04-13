using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Google.OrTools.LinearSolver;

namespace FactorioData
{
    public enum ConnectionState
    {
        Optimal,
        Overproduction,
        Starving
    }
    
    public class WorkspaceSolver : IDisposable
    {
        private struct VariableData
        {
            public NodeConfiguration node;
            public Variable variable;
            public int lastConstraint;
            public float objectiveCoef;
            public SolverParams param;
        }
        
        public enum SolverResult
        {
            None,
            Normal,
            WarningNotOptimal,
            WarningHasDeadlocks,
            ErrorOther,
            ErrorDoubleDeadlock,
        }
        
        private enum DeadlockType : byte
        {
            NotChecked, Checking, NoDeadlock, PossibleDeadlock
        }

        private struct ConnectionData
        {
            public ConnectionConfiguration connection;
            public Constraint constraint;
            public Variable slack;
            public SolverParams param;
            public DeadlockType deadlockType;
        }

        private readonly WorkspaceConfiguration workspace;
        private readonly Solver solver;
        private readonly Objective objective;
        
        private int variableCount, connectionCount;
        private VariableData[] variables;
        private ConnectionData[] connections;

        public WorkspaceSolver(WorkspaceConfiguration workspace)
        {
            this.workspace = workspace;
            solver = Solver.CreateSolver("WorkspaceSolver", "GLOP_LINEAR_PROGRAMMING");
            objective = solver.Objective();
        }

        // TODO replace recursion with iteration
        // TODO research other types of deadlocks
        private void DeadlockCheckVariable(ref VariableData variable)
        {
            foreach (var ingredient in variable.node.ingredients)
            {
                if (ingredient.connection != null)
                    DeadlockCheckConnection(ref connections[ingredient.connection.solverTag]);
            }
        }

        private void DeadlockCheckConnection(ref ConnectionData connection)
        {
            if (connection.deadlockType == DeadlockType.Checking)
                connection.deadlockType = DeadlockType.PossibleDeadlock;
            else if (connection.deadlockType == DeadlockType.NotChecked)
            {
                connection.deadlockType = DeadlockType.Checking;
                foreach (var port in connection.connection.ports)
                {
                    if (port.flowAmount > 0f)
                        DeadlockCheckVariable(ref variables[port.configuration.solverTag]);
                }

                if (connection.deadlockType == DeadlockType.Checking)
                    connection.deadlockType = DeadlockType.NoDeadlock;
            }
        }

        private void MarkPossibleDeadlocks()
        {
            for (var i = 0; i < variableCount; i++)
            {
                if (variables[i].param.min > 0)
                    DeadlockCheckVariable(ref variables[i]);
            }
        }

        private SolverResult TrySolve()
        {
            var sw = Stopwatch.StartNew();
            var status = solver.Solve();
            Console.WriteLine("GLOP computation completed in " +sw.ElapsedMilliseconds +"ms with result "+status);

            if (status == Solver.ResultStatus.INFEASIBLE)
            {
                Console.WriteLine("Model dump:\n"+solver.ExportModelAsLpFormat( false));
            }
            
            switch (status)
            {
                case Solver.ResultStatus.OPTIMAL: return SolverResult.Normal;
                case Solver.ResultStatus.FEASIBLE: return SolverResult.WarningNotOptimal;
                case Solver.ResultStatus.INFEASIBLE: return SolverResult.WarningHasDeadlocks;
                default: return SolverResult.ErrorOther;
            }
        }

        private void SolveThreaded()
        {
            try
            {
                var result = TrySolve();
                if (result == SolverResult.WarningHasDeadlocks)
                    result = SolveDeadlock();

                if (result < SolverResult.ErrorOther)
                {
                    for (var i = 0; i < variableCount; i++)
                    {
                        var variable = variables[i];
                        variable.node.SetComputationResult((float) variable.variable.SolutionValue());
                    }

                    for (var i = 0; i < connectionCount; i++)
                    {
                        var connection = connections[i];
                        var slack = connection.slack;
                        if (!ReferenceEquals(slack, null) && slack.SolutionValue() > 0)
                            connection.connection.SetSomputationState(ConnectionState.Starving);
                        else
                            connection.connection.SetSomputationState(connection.constraint.BasisStatus() == Solver.BasisStatus.AT_LOWER_BOUND
                                ? ConnectionState.Optimal
                                : ConnectionState.Overproduction);
                    }
                }

                FinishWithResult(result);
            }
            catch (Exception)
            {
                FinishWithResult(SolverResult.ErrorOther);
            }
        }

        private void FinishWithResult(SolverResult result)
        {
            workspace.solverResult = result;
            EnvironmentSettings.dispatcher.DispatchInMainThread(workspace.NewSolutionArrived);
        }

        public void Solve()
        {
            solver.Clear();
            variableCount = workspace.allNodes.Count;
            connectionCount = workspace.allConnections.Count;
            if (variables == null || variables.Length < variableCount)
                Array.Resize(ref variables, variableCount);
            if (connections == null || connections.Length < connectionCount)
                Array.Resize(ref connections, connectionCount);
            var localNodeId = 0;
            foreach (var node in workspace.allNodes)
            {
                node.solverTag = localNodeId;
                var param = node.GetSolverParams();
                var restricted = param.penalty == float.PositiveInfinity;
                variables[localNodeId] = new VariableData
                {
                    node = node,
                    variable = solver.MakeNumVar(param.min, restricted ? param.min : float.PositiveInfinity, node.varname),
                    objectiveCoef = param.penalty,
                    param = param
                };
                localNodeId++;
            }

            for (var i = 0; i < connectionCount; i++)
            {
                var connection = workspace.allConnections[i];
                connection.solverTag = i;
                var param = connection.GetSolverParams();
                var constraint = solver.MakeConstraint(param.min, param.max, "line_"+connection.type.type+"_"+connection.type.name);
                connections[i] = new ConnectionData
                {
                    connection = connection,
                    constraint = constraint,
                    param = param
                };
                foreach (var port in connection.ports)
                {
                    ref var variable = ref variables[port.configuration.solverTag];
                    if (variable.lastConstraint == (i + 1))
                    {
                        // Connection have more than 1 connections to a single node
                        var prev = constraint.GetCoefficient(variable.variable);
                        constraint.SetCoefficient(variable.variable, port.flowAmount + prev);
                    }
                    else
                    {
                        variable.lastConstraint = (i + 1);
                        constraint.SetCoefficient(variable.variable, port.flowAmount);
                    }
                    variables[port.configuration.solverTag].objectiveCoef += param.penalty * port.flowAmount;
                }
            }

            MarkPossibleDeadlocks();
            objective.SetMinimization();
            for (var i = 0; i < variableCount; i++)
            {
                var var = variables[i];
                if (var.objectiveCoef != 0)
                    objective.SetCoefficient(var.variable, var.objectiveCoef);
            }

            Task.Run(SolveThreaded);
        }

        private SolverResult SolveDeadlock()
        {
            // adding slack to some constraints and now trying to minimize slack
            objective.Clear();
            objective.SetMinimization();
            for (var i = 0; i < connectionCount; i++)
            {
                if (connections[i].deadlockType == DeadlockType.PossibleDeadlock)
                {
                    var slack = solver.MakeNumVar(0, double.PositiveInfinity, "__slack");
                    connections[i].slack = slack;
                    connections[i].constraint.SetCoefficient(slack, 1);
                    objective.SetCoefficient(slack, 1);
                }
            }

            var status = TrySolve();
            if (status >= SolverResult.ErrorOther)
                return status;
            if (status == SolverResult.WarningHasDeadlocks)
                return SolverResult.ErrorDoubleDeadlock;
            return SolverResult.WarningHasDeadlocks;
        }

        public void Dispose()
        {
            solver?.Dispose();
        }
    }
}