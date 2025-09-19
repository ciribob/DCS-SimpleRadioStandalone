using System;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages
{
    /// <summary>
    /// Message for communicating server errors from background threads to the main thread/UI.
    /// </summary>
    public class ServerErrorMessage
    {
        public string Error { get; }
        public Exception? Exception { get; }

        public ServerErrorMessage(string error, Exception? exception = null)
        {
            Error = error;
            Exception = exception;
        }
    }
}