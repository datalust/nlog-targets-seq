using System;
using NLog;

namespace Example
{
    class Program
    {
        static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public static void Main()
        {
            const string server = "Seq", library = "NLog";

            // Structured logging: two named properties are captured using the message template:
            logger.Info("Hello, {Server}, from {Library}", server, library);

            // Text logging: the two properties are captured using positional arguments:
            logger.Info("Goodbye, {0}, from {1}", server, library);

            // Complex data can be captured and serialized into the event using the `@` directive:
            logger.Info("Current user is {@User}", new { Name = Environment.UserName, Tags = new[]{ 1, 2, 3 }});

            Console.ReadKey();
        }
    }
}
