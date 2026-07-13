using System;

namespace GpgPatcher
{
    internal sealed class FriendlyException : Exception
    {
        public FriendlyException(string message)
            : base(message)
        {
        }

        public FriendlyException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
