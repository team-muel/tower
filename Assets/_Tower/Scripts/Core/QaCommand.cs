namespace Tower.Core
{
    public enum QaCommandKind
    {
        Press,
        State,
        Scene,
        Quit
    }

    // Parsed QA harness command. Argument is empty for state/quit.
    public readonly struct QaCommand
    {
        public QaCommand(QaCommandKind kind, string argument)
        {
            Kind = kind;
            Argument = argument ?? string.Empty;
        }

        public QaCommandKind Kind { get; }
        public string Argument { get; }
    }
}
