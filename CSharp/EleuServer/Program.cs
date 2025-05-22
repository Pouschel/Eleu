using EmbedIO.Files;
using EmbedIO;
using System.Reflection;
using EmbedIO.Routing;
using EmbedIO.WebApi;

namespace EleuServer;

internal class Program
{
  static WebServer? server;
  public static void LogInfo(string str) => Console.WriteLine(str);

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
    Start(1754,  "/_framework", d2, "/", d1);
    Console.ReadLine();
  }

  public static void Start(int port, params string[] webDirs )
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
    for (int i = 0; i < webDirs.Length; i+=2)
    {
      string srvDir=webDirs[i];
      string? webDir = webDirs[i+1];
      LogInfo($"WebDir: {srvDir}->{webDir}");
      server.WithStaticFolder(srvDir, webDir, true, m => ConfigFileModule(m));
    }

    void ConfigFileModule(FileModule m)
    {
      //if (debugMode) 
      //m.WithContentCaching(false);
    }

   // Swan.Logging.Logger.UnregisterLogger<Swan.Logging.ConsoleLogger>();
    //start the web server
    server.Start();
  }
}

class WebApi : WebApiController
{

  [Route(HttpVerbs.Get, "/HandleSource/{fileName}/{text}")]
  public string HandleSource(string fileName, string text) { return ""; }

}