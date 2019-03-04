using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.IO;

namespace MergeMediaFoldersConsole
{
    public class MergeAction
    {
        private MergeOptions Options { get; set; }
        private NLog.ILogger logger { get; set; }

        private readonly photo.exif.Parser ExifParser = new photo.exif.Parser();

        public MergeAction(MergeOptions opts)
        {
            Options = opts;
            logger = NLog.LogManager.GetCurrentClassLogger();
        }

        public int MergeAndReturnExitCode()
        {
            logger.Info($"Meging source folders into output folder.");
            logger.Info($"Output folder: {Options.DestinationFolder}\\{Options.DestinationPathFormat}");
            var listOfSourceFolders = Options.SourceFolders.Aggregate("", (c, n) => c + ", " + n).Substring(2);
            logger.Info($"Source folders: {listOfSourceFolders}");

            foreach(var s in Options.SourceFolders)
            {
                logger.Info($"Processing source: {s}...");
                var folder = new DirectoryInfo(s);
                MergeFromSourceFolder(folder);
            }
            return 1;
        }

        public void MergeFromSourceFolder(DirectoryInfo sourceFolder)
        {
            if (!sourceFolder.Exists)
            {
                logger.Error($"Source directory {sourceFolder.FullName} no longer exists.");
                return;
            }

            var files = sourceFolder.EnumerateFiles().OrderBy(x => x.CreationTimeUtc);
            DateTimeOffset? previousDateTimeTaken = null;
            foreach(var file in files)
            {
                try
                {
                    MergeFileIntoOutput(file, ref previousDateTimeTaken);
                }
                catch (Exception ex)
                {
                    logger.Trace(ex);
                    logger.Error($"An exception occured merging {file.Name}: {ex.Message}.");
                }
            }

            if (Options.Recurive)
            {
                var subfolders = sourceFolder.GetDirectories();
                foreach(var subfolder in subfolders)
                {
                    MergeFromSourceFolder(subfolder);
                }
            }
        }

        private const int ExifDateTime = 0x0132;
        private const int ExifDTOrig = 0x9003;
        private const int ExifDTDigitized = 0x9004;

        private void MergeFileIntoOutput(FileInfo file, ref DateTimeOffset? suggestedDateTime)
        {
            DateTimeOffset captureDate;
            if (Options.UseCaptureDate && TryParseCaptureDateTime(file, out captureDate))
            {
                suggestedDateTime = captureDate;
                MergeIntoDestination(file, captureDate);
                return;
            }


            if (Options.UseSuggestedDate 
                && suggestedDateTime.HasValue 
                && suggestedDateTime.Value != DateTimeOffset.MinValue)
            {
                logger.Info($"Using suggested date {suggestedDateTime} for {file.Name}.");
                MergeIntoDestination(file, suggestedDateTime.Value);
                return;
            }

            if (Options.UseDateCreated)
            {
                MergeIntoDestination(file, new DateTimeOffset(file.CreationTime));
                return;
            }

            if (Options.UseDateModified)
            {
                MergeIntoDestination(file, new DateTimeOffset(file.LastWriteTime));
                return;
            }

            logger.Info($"Unable to determine destination for {file.FullName}");
        }

        private void MergeIntoDestination(FileInfo file, DateTimeOffset captureDateTime)
        {
            string destFullName = Path.Combine(Options.DestinationFolder, 
                GenerateFolderName(Options.DestinationPathFormat, captureDateTime), 
                file.Name);

            if (File.Exists(destFullName))
            {
                switch(Options.ExistingDestinationFile)
                {
                    case FileExistsOperation.DeleteSource:
                        logger.Debug($"Destination already exists for {file.FullName}. Deleting source file.");
                        file.Delete();
                        break;

                    case FileExistsOperation.OverwriteDestinationAlways:
                        PerformMergeOperation(file, destFullName, true);
                        break;

                    case FileExistsOperation.OverwriteDestinationIfIdentical:
                        if (CheckIfFilesMatch(file, new FileInfo(destFullName), Options.UseBinaryComparison))
                        {
                            PerformMergeOperation(file, destFullName, true);
                        }
                        else
                        {
                            logger.Info($"Skipped {file.FullName} -- destination file exists and was not identical.");
                        }
                        break;

                    case FileExistsOperation.Skip:
                        logger.Debug($"Destination already exists for {file.FullName}. Skipped.");
                        break;
                    default:
                        logger.Error($"Unhandled ExistingDestinationFile Mode: {Options.ExistingDestinationFile}.");
                        break;
                }
            }
            else
            {
                PerformMergeOperation(file, destFullName, false);
            }
        }

        private void PerformMergeOperation(FileInfo source, string destination, bool overwrite = false)
        {
            try
            {
                switch (Options.MergeOperation)
                {
                    case FileModeOperation.Copy:
                        logger.Debug($"Copying {source.FullName} to {destination}.");
                        source.CopyTo(destination, overwrite);
                        break;

                    case FileModeOperation.Move:
                        logger.Debug($"Moving {source.FullName} to {destination}.");
                        source.CopyTo(destination, overwrite);
                        source.Delete();
                        break;

                    case FileModeOperation.DryRun:
                        logger.Info($"DryRun: Moving {source.FullName} to {destination}.");
                        break;

                    default:
                        logger.Warn($"Unhandled merge operation: {Options.MergeOperation}.");
                        break;
                }
            } catch (IOException ex)
            {
                logger.Warn(ex, "Unable to complete the file operation, an exception occured.");
            }
        }

        private bool CheckIfFilesMatch(FileInfo sourceFile, FileInfo destFile, bool checkFileContents)
        {
            if (sourceFile.Length != destFile.Length)
            {
                logger.Debug($"sourceFile.Length != destFile.length {sourceFile.Length},{destFile.Length}");
                return false;
            }

            if (sourceFile.LastWriteTimeUtc != destFile.LastWriteTimeUtc)
            {
                logger.Debug($"sourceFile.lastWriteTime != destFile.lastWriteTime {sourceFile.LastWriteTimeUtc}, {destFile.LastWriteTimeUtc}");
                return false;
            }

            if (checkFileContents)
            {
                var sourceHash = ComputeHash(sourceFile);
                if (sourceHash == null)
                {
                    return false;
                }
                var destHash = ComputeHash(destFile);
                if (destHash == null || sourceHash != destHash)
                {
                    logger.Debug("sourceFile.hash and destFile.hash were different.");
                    return false;
                }
            }

            return true;
        }

        private string ComputeHash(FileInfo file)
        {
            var sha = System.Security.Cryptography.SHA256.Create();
            try
            {
                using (FileStream stream = file.OpenRead())
                {
                    byte[] bytes = sha.ComputeHash(stream);
                    string hash = "";
                    foreach (byte b in bytes)
                    {
                        hash += b.ToString("x2");
                    }
                    return hash;
                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex, $"Unable to compute hash for {file.FullName}.");
            }
            return null;
        }

        private string GenerateFolderName(string destinationPathFormat, DateTimeOffset captureDateTime)
        {
            var regex = new System.Text.RegularExpressions.Regex("({.*?})");
            var keys = regex.Split(destinationPathFormat).ToList();

            for(int index = 0; index < keys.Count; index++)
            {
                if (keys[index].StartsWith("{") && keys[index].EndsWith("}"))
                {
                    keys[index] = captureDateTime.ToString(keys[index]);
                }
            }

            var output = string.Concat(keys);
            logger.Debug($"Generated destination folder \"{output}\" for {captureDateTime}.");
            return output;
        }

        private bool TryParseCaptureDateTime(FileInfo file, out DateTimeOffset captureDateTime)
        {


            captureDateTime = DateTimeOffset.MinValue;
            IEnumerable<photo.exif.ExifItem> data = new photo.exif.ExifItem[0];
            try
            {
                data = ExifParser.Parse(file.FullName);
            }
            catch (Exception ex)
            {
                logger.Debug(ex, "Unable to parse exif data.");
                logger.Warn($"An error occured parsing exif data: {ex.Message}");
            }

            var q = (from d in data
                     where d.Id == ExifDateTime || d.Id == ExifDTOrig || d.Id == ExifDTDigitized
                     orderby d.Id
                     select d).FirstOrDefault();

            if (q != null)
            {
                logger.Debug($"Found exif tag: {q.Id:x4} value: {q.Value}.");
                string blank = "\u2000";
                string format = $"yyyy:MM:dd{blank}HH:mm:ss";

                DateTimeOffset result = DateTimeOffset.MinValue;
                if (DateTimeOffset.TryParseExact(q.Value as string, format, System.Globalization.CultureInfo.InvariantCulture.DateTimeFormat, System.Globalization.DateTimeStyles.AssumeLocal, out result))
                {
                    logger.Debug($"Parsed value {q.Value} as {result}.");
                    captureDateTime = result;
                    return true;
                }

                logger.Warn($"Unable to parse found exif tag data: {q.Id:x4} - {q.Value}.");
                return false;
            }

            logger.Debug("No date/time values were found in the exif data for the file.");
            return false;
        }
    }
}
