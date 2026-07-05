namespace Tower.Core
{
    public sealed class SimCliResult
    {
        public SimCliResult(string outputPath, AutoBattleSimulationResult simulation)
        {
            OutputPath = outputPath;
            Simulation = simulation;
        }

        public string OutputPath { get; }
        public AutoBattleSimulationResult Simulation { get; }
    }
}
