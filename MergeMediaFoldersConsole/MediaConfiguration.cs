using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using System.IO;

namespace MergeMediaFoldersConsole
{
    class MediaConfiguration
    {
        public MediaConfiguration()
        {
            ImageFileExtensions = new Dictionary<string, object>();
            VideoFileExtensions = new Dictionary<string, object>();
        }

        public Dictionary<string, object> ImageFileExtensions { get; set; }

        public Dictionary<string, object> VideoFileExtensions { get; set; }

        public static void BulkAddExtensions(Dictionary<string,object> dict, MediaFormatOptions options, params string[] extensions)
        {
            foreach(var ext in extensions)
            {
                dict.Add(ext, new object());
            }
        }

        public static MediaConfiguration Load(string source)
        {
            if (source.StartsWith("~/"))
            {
                // Rewrite to the application's binary folder
                var myAssemblyPath = System.Reflection.Assembly.GetAssembly(typeof(Program)).Location;
                var myDirectory = Path.GetDirectoryName(myAssemblyPath);
                source = Path.Combine(myDirectory, source.Substring(2));
            }

            var data = File.ReadAllText(source);
            try
            {
                return JsonConvert.DeserializeObject<MediaConfiguration>(data);
            }
            catch (Exception ex)
            {
                var logger = NLog.LogManager.GetCurrentClassLogger();
                logger.Warn(ex, $"Unable to load configuration file {source}.");
            }
            return MediaConfiguration.Default;
        }

        public static MediaConfiguration Default
        {
            get
            {
                var defConfig = new MediaConfiguration();
                BulkAddExtensions(defConfig.ImageFileExtensions, new MediaFormatOptions { ExifCompatible = true }, "jpg", "jpeg", "png", "gif");
                BulkAddExtensions(defConfig.VideoFileExtensions, new MediaFormatOptions(), "mp4", "mov", "avi", "3gp");
                return defConfig;
            }
        }
            
    }

    class MediaFormatOptions
    {
        [JsonProperty("exif")]
        public bool ExifCompatible { get; set; }
    }


}
