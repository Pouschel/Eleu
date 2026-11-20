using System.Globalization;
using System.Runtime.CompilerServices;

namespace Eleu.Tests;

internal static class TestSetup
{
  /// <summary>All tests run in German, like the browsers of the pupils.</summary>
  public static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

#pragma warning disable CA2255 // ModuleInitializer in a test assembly is intended
  [ModuleInitializer]
  internal static void Init()
  {
    CultureInfo.DefaultThreadCurrentCulture = German;
    CultureInfo.DefaultThreadCurrentUICulture = German;
    CultureInfo.CurrentCulture = German;
    CultureInfo.CurrentUICulture = German;
  }
#pragma warning restore CA2255
}
