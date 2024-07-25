
namespace Eleu;

public class EleuOptions
{
  public bool DumpStackOnError = true;
  public bool UseDebugger = false;
  public bool ThrowOnAssert = false;
  public TextWriter Out = TextWriter.Null;
  public TextWriter Err = TextWriter.Null;
  public bool PrintByteCode = false;
  public bool OnlyFirstError = false;
  /// <summary>
  /// show the call stack in case of a runtime error
  /// </summary>
  public string DumpFileName = "";

  public void WriteCompilerError(in InputStatus status, string message)
  {
    var msg = string.IsNullOrEmpty(status.FileName) ? message : $"{status.Message}: Cerr: {message}";
    Err.WriteLine(msg);
    System.Diagnostics.Trace.WriteLine(msg);
  }
}
