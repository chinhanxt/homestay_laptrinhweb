using System;

namespace WebHomestay.Services.AI
{
    public sealed class EmbeddingUnavailableException : Exception
    {
        public EmbeddingUnavailableException(string message)
            : base(message)
        {
        }

        public EmbeddingUnavailableException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
