using System;

namespace Tower.Data
{
    // Thrown once, with ALL collected load-time violations aggregated into a
    // single message. Never fail silently — bad static data fails loudly here.
    public sealed class DataValidationException : Exception
    {
        public DataValidationException(string message) : base(message)
        {
        }
    }
}
