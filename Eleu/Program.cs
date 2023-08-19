
namespace Eleu;

public class EleuOptions
{
	public bool DumpStackOnError = true;
	public bool UseDebugger = false;
	public bool ThrowOnAssert = false;
	public TextWriter Out = TextWriter.Null;
	public TextWriter Err = TextWriter.Null;
	public bool PrintByteCode=false;
	public bool UseInterpreter = true;
	public bool OnlyFirstError = false;

	public void WriteCompilerError(in InputStatus status, string message)
	{
		var msg = string.IsNullOrEmpty(status.FileName) ? message : $"{status.Message}: Cerr: {message}";
		Err.WriteLine(msg);
		System.Diagnostics.Trace.WriteLine(msg);
	}
}

public static class Statics
{
	public static T RemoveLast<T>(this IList<T> list)
	{
		int idx = list.Count - 1;
		var item = list[idx];
		list.RemoveAt(idx);
		return item;
	}
}
