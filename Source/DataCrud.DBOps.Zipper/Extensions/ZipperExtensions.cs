namespace DataCrud.DBOps.Zipper.Extensions
{
    public static class ZipperExtensions
    {
        public static string ToZip(this string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return fileName;
            var extension = System.IO.Path.GetExtension(fileName);
            return string.IsNullOrEmpty(extension) ? fileName + ".zip" : fileName.Replace(extension, ".zip");
        }
    }
}

