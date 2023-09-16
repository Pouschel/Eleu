using System.IO.Compression;
using System.Text;

namespace Ts.IO;

class FileUtils
{
  public static string CompressBase64(byte[] buffer)
  {
    var memStream = new MemoryStream();
    using (var zipStream = new GZipStream(memStream, CompressionMode.Compress))
    {
      zipStream.Write(buffer, 0, buffer.Length);
    }
    return Convert.ToBase64String(memStream.ToArray());
  }
  public static string CompressBase64(string s) => CompressBase64(Encoding.UTF8.GetBytes(s));
  public static string DecompressBase64(string s)
  {
    var buffer = Convert.FromBase64String(s);
    var coSt = new MemoryStream(buffer);
    var ucoSt = new MemoryStream();

    using (var gzip = new GZipStream(coSt, CompressionMode.Decompress))
    {
      gzip.CopyTo(ucoSt);
    }
    return Encoding.UTF8.GetString(ucoSt.ToArray());
  }
}


