namespace DataCrud.DBOps.Zipper.Extensions
{
    public static class ZipperExtensions
    {
        public static string BakToZip(this string fileName)
        {
            return string.IsNullOrEmpty(fileName) ? fileName : fileName.Replace(".bak", ".zip");
        }
    }
}

