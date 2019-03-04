using System;
using System.Collections.Generic;
using System.Text;
using CommandLine;

namespace MergeMediaFoldersConsole
{

    [Verb("merge", HelpText = "Merge the contents of source folders into the output folder.")]
    public class MergeOptions : DestinationArguments
    {
        [Option('m', "merge-style",
            Default = FileModeOperation.Copy,
            HelpText = "Style of merge used (Copy, Move, or DryRun.")]
        public FileModeOperation MergeOperation { get; set; }

        [Option('s', "source", HelpText = "Pathes to one or more sources of media files.")]
        public IEnumerable<string> SourceFolders { get; set; }

        [Option('r', "recursive", HelpText = "Recurse through the source folder and children folders to find all media files.")]
        public bool Recurive { get; set; }

        [Option("use-capture-date",
            HelpText = "Use the EXIF capture date for media to determine destination folder (always used first).",
            Default = true)]
        public bool UseCaptureDate { get; set; }

        [Option("use-suggested-date",
            HelpText = "Use the date of nearby files if capture date is unavailable.",
            Default = false)]
        public bool UseSuggestedDate { get; set; }

        [Option("use-created-date",
            HelpText = "Use the file-creation date to determine destination folder.",
            Default = true,
            SetName = "FileDateProperty")]
        public bool UseDateCreated { get; set; }

        [Option("use-modified-date",
            HelpText = "Use the file-modification date to determine destination folder.",
            Default = false,
            SetName = "FileDateProperty")]
        public bool UseDateModified { get; set; }

    }

    public class DestinationArguments : ProgramArguments
    {
        [Option('o', "output", Required = true, HelpText = "Set the root output folder")]
        public string DestinationFolder { get; set; }

        [Option('f', "folder-format",
            Default = @"{YYYY}\{YYYY}-{MM}-{MMMM}",
            HelpText = "String format used for folders in the output destination.")]
        public string DestinationPathFormat { get; set; }

        [Option("exists", 
            HelpText = "Decide how to handle existing files: Skip, OverwriteDestination, DeleteSource", 
            Default = FileExistsOperation.Skip)]
        public FileExistsOperation ExistingDestinationFile { get; set; }

        [Option("binary", 
            HelpText = "When comparing existing files, evaluate the contents of the file in addition to the metadata.",
            Default = false)]
        public bool UseBinaryComparison { get; set; }
    }

    public class ProgramArguments
    {
        [Option('v', "verbose", HelpText = "Enables verbose logging output to stdout.", Default = false)]
        public bool Verbose { get; set; }

        [Option("config",
            HelpText = "Path to configuration file.",
            Default = "~/configuration.json")]
        public string ConfigurationFile { get; set; }

        public virtual void LogArguments()
        {
            var logger = NLog.LogManager.GetCurrentClassLogger();
            logger.Debug(Newtonsoft.Json.JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented));
        }
    }

    public enum FileModeOperation
    {
        DryRun,
        Copy,
        //CopyAndRecycle,
        Move
    }

    public enum FileExistsOperation
    {
        Skip,
        OverwriteDestinationIfIdentical,
        OverwriteDestinationAlways,
        DeleteSource
    }
}
