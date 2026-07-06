namespace Tower.Core
{
    // Response formatting for the line-oriented QA protocol.
    public static class QaProtocol
    {
        public const string Ok = "OK";

        public static string Error(string reason)
        {
            var text = string.IsNullOrWhiteSpace(reason) ? "unknown" : reason.Trim();
            return "ERR " + text.Replace("\r", " ").Replace("\n", " ");
        }
    }
}
