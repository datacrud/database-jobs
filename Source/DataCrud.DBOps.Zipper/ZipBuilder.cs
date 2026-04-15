using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace DataCrud.DBOps.Zipper
{
    public static class ZipBuilder
    {

        public static string Zip(string fileName, List<string> files)
        {
            Console.WriteLine("Start zip...");

            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }

            // Create and open a new ZIP file
            var zip = ZipFile.Open(fileName, ZipArchiveMode.Create);
            foreach (var file in files)
            {
                Console.WriteLine($"Creating zip to {file}");

                // Add the entry for each file
                zip.CreateEntryFromFile(file, Path.GetFileName(file), CompressionLevel.Optimal);
            }
            // Dispose of the object when we are done
            zip.Dispose();

            return fileName;
        }

        public static Stream Zip(List<ZipItem> zipItems)
        {
            var zipStream = new MemoryStream();

            using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
            {
                foreach (var zipItem in zipItems)
                {
                    var entry = zip.CreateEntry(zipItem.Name);
                    using (var entryStream = entry.Open())
                    {
                        zipItem.Content.CopyTo(entryStream);
                    }
                }
            }
            zipStream.Position = 0;
            return zipStream;
        }


        public static byte[] ZipAsByte(List<ZipItem> zipItems)
        {
            var zipStream = new MemoryStream();

            using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
            {
                foreach (var zipItem in zipItems)
                {
                    var entry = zip.CreateEntry(zipItem.Name);
                    using (var entryStream = entry.Open())
                    {
                        zipItem.Content.CopyTo(entryStream);
                    }
                }
            }
            zipStream.Position = 0;

            var bytes = ReadAsByte(zipStream);

            return bytes;
        }

        public static byte[] ReadAsByte(Stream input)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                input.CopyTo(ms);
                return ms.ToArray();
            }
        }
    }
}
