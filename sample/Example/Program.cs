using System;
using NLog;

namespace Example
{
    static class Program
    {
        static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static void Main()
        {
            const string server = "Seq", library = "NLog";

            // Structured logging: two named properties are captured using the message template:
            Logger.Info("Hello, {Server}, from {Library}", server, library);

            // Text logging: the two properties are formatted into the message using positional arguments:
            Logger.Info("Goodbye, {0}, from {1}", server, library);

            // Complex data can be captured and serialized into the event using the `@` directive:
            Logger.Info("Current user is {@User}, height {Height:0.00}", new { Name = Environment.UserName, Tags = new[]{ 1, 2, 3 }}, 123.4567);

            // Simple events are still accepted
            Logger.Info("Simple message");

            // As are objects
            Logger.Info(new object());
        }
    }
}
