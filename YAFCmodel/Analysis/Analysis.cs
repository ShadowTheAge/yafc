namespace YAFC.Model
{
    public abstract class Analysis
    {
        public static void RegisterAnalysis(Analysis analysis, params Analysis[] dependencies)
        {
            
        }

        public abstract void Compute();
    }
}