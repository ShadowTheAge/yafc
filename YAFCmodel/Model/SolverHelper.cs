using System.Collections.Generic;
using Google.OrTools.LinearSolver;

namespace YAFC.Model {
    public class SolverHelper<TVariable, TConstraint> {
        private readonly Dictionary<(TVariable var, TConstraint constr), float> values = new Dictionary<(TVariable, TConstraint), float>();
        private readonly List<(TVariable var, float min, float max, float coef)> variables = new List<(TVariable var, float min, float max, float coef)>();
        private readonly List<(TConstraint constr, float min, float max)> constraints = new List<(TConstraint constr, float min, float max)>();
        private readonly bool maximize;

        private readonly Dictionary<TVariable, float> results = new Dictionary<TVariable, float>();

        public SolverHelper(bool maximize) {
            this.maximize = maximize;
        }

        public float this[TVariable var, TConstraint constr] {
            get => values.TryGetValue((var, constr), out var val) ? val : 0;
            set => values[(var, constr)] = value;
        }

        public float this[TVariable var] => results.TryGetValue(var, out var value) ? value : 0f;

        public void AddVariable(TVariable var, float min, float max, float coef) {
            variables.Add((var, min, max, coef));
        }

        public void AddConstraint(TConstraint constr, float min, float max) {
            constraints.Add((constr, min, max));
        }

        public void Clear() {
            values.Clear();
            variables.Clear();
            constraints.Clear();
        }

        public Solver.ResultStatus Solve(string name) {
            var solver = DataUtils.CreateSolver(name);
            results.Clear();
            var realMapVars = new Dictionary<TVariable, Variable>(variables.Count);
            var realMapConstrs = new Dictionary<TConstraint, Constraint>(constraints.Count);
            var objective = solver.Objective();
            objective.SetOptimizationDirection(maximize);

            foreach (var (tvar, min, max, coef) in variables) {
                var variable = solver.MakeNumVar(min, max, tvar.ToString());
                objective.SetCoefficient(variable, coef);
                realMapVars[tvar] = variable;
            }

            foreach (var (tconst, min, max) in constraints) {
                var constraint = solver.MakeConstraint(min, max, tconst.ToString());
                realMapConstrs[tconst] = constraint;
            }

            foreach (var ((tvar, tconstr), value) in values) {
                if (realMapVars.TryGetValue(tvar, out var variable) && realMapConstrs.TryGetValue(tconstr, out var constraint))
                    constraint.SetCoefficient(variable, value);
            }

            var result = solver.Solve();

            if (result is Solver.ResultStatus.OPTIMAL or Solver.ResultStatus.FEASIBLE) {
                foreach (var (tvar, var) in realMapVars)
                    results[tvar] = (float)var.SolutionValue();
            }
            solver.Dispose();

            return result;
        }
    }
}
