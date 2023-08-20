//https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-interop?view=aspnetcore-7.0

using System;
using System.Runtime.InteropServices.JavaScript;


class Program
{
  public static void Main()
  {

  }
}

                                  
public partial class MyClass        
{
    [JSExport]
    internal static string Greeting()
    {
        var text = $"Hello, World! Greetings from {GetHRef()}";
        Console.WriteLine(text);
        return text;
    }                                
                                
    [JSImport("window.location.href", "main.js")]          
    internal static partial string GetHRef();
}

public partial class App
{
  [JSExport]
  internal static void Test()
  {
    Console.WriteLine("in test");
  }
}