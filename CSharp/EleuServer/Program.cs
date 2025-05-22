using EmbedIO.Files;
using EmbedIO;
using System.Reflection;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using System.Web;

namespace EleuServer;

internal class Program
{
  static WebServer? server;
  public static void LogInfo(string str) => Console.WriteLine($"{DateTime.Now}: {str}");

  static void Main(string[] args)
  {
    Console.WriteLine("Eleu WebServer supporting synced file editing.");
    var ass = Assembly.GetEntryAssembly();
    var dir = Path.GetDirectoryName(ass!.Location);
    for (int i = 0; i < 4; i++)
    {
      dir = Path.GetDirectoryName(dir);
    }
    dir = Path.Combine(dir!, "EleuWeb");
    var d1 = Path.Combine(dir, "wwwroot");
    var d2 = Path.Combine(dir, @"bin\debug\net9.0\wwwroot\_framework");
    //var d2 = Path.Combine(dir, @"bin\Release\net9.0");
    Start(1754, "/_framework", d2, "/", d1);
    Console.ReadLine();
  }

  public static void Start(int port, params string[] webDirs)
  {
    if (server != null)
      return;
    var webPort = port;

    bool debugMode = System.Diagnostics.Debugger.IsAttached;
    //if (debugMode) AnirApp.UseDebugDir = true;

    var url = string.Format("http://+:{0}/", webPort);
    var mode = HttpListenerMode.EmbedIO;

    server = new WebServer(o => o
        .WithUrlPrefix(url)
        .WithMode(mode));

    server.WithWebApi("/api", m => m.WithController<WebApi>());
    for (int i = 0; i < webDirs.Length; i += 2)
    {
      string srvDir = webDirs[i];
      string? webDir = webDirs[i + 1];
      LogInfo($"WebDir: {srvDir}->{webDir}");
      server.WithStaticFolder(srvDir, webDir, true, m => ConfigFileModule(m));
    }

    void ConfigFileModule(FileModule m)
    {
      if (debugMode)
        m.WithContentCaching(false);
    }

    Swan.Logging.Logger.UnregisterLogger<Swan.Logging.ConsoleLogger>();
    //start the web server
    server.Start();
  }

  static string lastFn = "", lastText = "";
  internal static string HandleFile(string fn, string text)
  {
    // Check for external changed text
    if (!File.Exists(fn))
    {
      LogInfo($"File '{fn}' does not exist:"); return "";
    }
    var externalText = File.ReadAllText(fn);
    if (externalText != lastText)
    {
      LogInfo($"File '{fn}' externally changed. Reloading!");
      lastText = externalText; lastFn = fn;
      return externalText;
    }
    if (lastFn == fn && lastText == text) return "";
    File.WriteAllText(fn, text);
    Console.WriteLine($"File '{fn}' saved ({text.Length} chars)");
    lastText = text; lastFn = fn;
    return "";
  }

  internal static string LoadFile(string? fn)
  {
    if (fn == null) return "";
    if (!File.Exists(fn))
    {
      LogInfo($"File not found: '{fn}'"); return "";
    }
    lastText = File.ReadAllText(fn);
    lastFn = fn;
    LogInfo($"File '{fn}' loaded.");
    return lastText;
  }
}

class WebApi : WebApiController
{
  static string? GetFileName(string surl)
  {
    var qdict = HttpUtility.ParseQueryString(surl);
    var fn = qdict.Get("f");
    return fn;
  }
  [Route(HttpVerbs.Get, "/HandleSource/{surl}/{text}")]
  public string HandleSource(string surl, string text)
  {
    var fn = GetFileName(surl);
    if (fn == null) return "";
    return Program.HandleFile(fn, text);
  }

  [Route(HttpVerbs.Get, "/LoadFile/{surl}")]
  public string LoadFile(string surl)
  {
    var fn = GetFileName(surl);
    return Program.LoadFile(fn);
  }
}