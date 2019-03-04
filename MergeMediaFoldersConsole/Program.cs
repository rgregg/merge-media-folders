using System;
using CommandLine;

namespace MergeMediaFoldersConsole
{
    class Program
    {
        static int Main(string[] args)
        {
            // Configure NLog
            var config = new NLog.Config.LoggingConfiguration();
            var console = new NLog.Targets.ColoredConsoleTarget() { Layout = "${message}" };
            config.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Fatal, console);
            NLog.LogManager.Configuration = config;

            var logger = NLog.LogManager.GetCurrentClassLogger();
            logger.Info($"MergeMediaFoldersConsole v{Version}");

            // Parse command line options and execute the request
            int returnCode = 0;
            if (args.Length == 0)
            {
                logger.Error("Required command line arguments were missing.");
                returnCode = 2;
            }
            else
            {
                returnCode = Parser.Default.ParseArguments<MergeOptions>(args)
                    .MapResult(
                        (MergeOptions opts) => {
                            if (opts.Verbose)
                            {
                                config.AddRule(NLog.LogLevel.Trace, NLog.LogLevel.Debug, console);
                            }
                            opts.LogArguments();
                            return new MergeAction(opts).MergeAndReturnExitCode();
                            },
                        errs =>
                        {
                            foreach(var e in errs)
                            {
                                logger.Error("Unable to parse command line arguments.");
                                logger.Error(e);
                            }
                            return 1;
                        }
                    );
            }

            // Catch the end of the program in the debugger.
            if (System.Diagnostics.Debugger.IsAttached)
            {
                Console.WriteLine($"Exit code: {returnCode}");
                Console.Write("Press any key to end.");
                Console.Read();
            }
            return returnCode;
        }

        private static string Version
        {
            get
            {
                return "1.0";
            }
        }
    }
}
