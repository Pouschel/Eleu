using Eleu.Testing;

namespace Eleu.Tests;

public enum Engine { Interpreter, Vm }

/// <summary>
/// Every script in Eleu/Tests, run like EleuTester does: the output must match the expectations in its comments
/// (// expect:, //?, //Rerr:, //Cerr:) and no assert may fail.
/// </summary>
public class ScriptFileTests
{
  public static TheoryData<string, Engine> Cases()
  {
    var data = new TheoryData<string, Engine>();
    foreach (var file in TestFiles.All)
    {
      data.Add(file, Engine.Interpreter);
      data.Add(file, Engine.Vm);
    }
    return data;
  }

  [Fact]
  public void Configured_test_folders_exist()
  {
    Assert.NotEmpty(TestFiles.Dirs);
    Assert.All(TestFiles.Dirs, dir => Assert.True(Directory.Exists(dir), $"Test folder not found: {dir} (see TestDirs.txt / TestDirs.local.txt)"));
  }

  [Theory]
  [MemberData(nameof(Cases))]
  public void Output_matches_expectations(string file, Engine engine)
  {
    var path = TestFiles.FullPath(file);
    var source = File.ReadAllText(path);
    var sw = new StringWriter();
    var opt = new EleuOptions()
    {
      Out = sw,
      Err = sw,
      DumpStackOnError = false,
      UseDebugger = true,
      ThrowOnAssert = true,
    };
    try
    {
      Globals.CompileAndRunAst(source, path, opt, engine == Engine.Vm);
    }
    catch (EleuAssertionFail ex)
    {
      var line = ex.Status.HasValue ? ex.Status.Value.LineStart : 1;
      ScriptLocation.Fail(file, line, $"assert in Zeile {line}: {ex.Message}\n{sw}");
    }
    var actual = ScriptExpectation.NormalizeOutput(sw.ToString()).ReplaceLineEndings().TrimEnd();
    var expected = ScriptExpectation.ExpectedOutput(source).ReplaceLineEndings().TrimEnd();
    ScriptLocation.Equal(file, source, expected, actual);
  }
}
