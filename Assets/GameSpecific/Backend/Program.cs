namespace GameSpecific.Backend
{
    using System;
    using System.Threading;

    static class Program
    {
        static void Main()
        {
            var host = GameBackendSetup.CreateHost(7777);
            host.Listen(7777);

            Console.WriteLine("Backend server listening on port 7777...");
            Console.WriteLine("Press Ctrl+C to stop.");

            var running = true;
            Console.CancelKeyPress += (s, e) => { running = false; e.Cancel = true; };

            try
            {
                while (running)
                {
                    host.Tick();
                    Thread.Sleep(15); // ~66 fps
                }
            }
            finally
            {
                host.Halt();
                Console.WriteLine("Backend server stopped.");
            }
        }
    }
}
