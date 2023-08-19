
using GenerateAst;

static class Extensions
{
	public static void println(this TextWriter tw, string s = "") => tw.WriteLine(s);

}

public class Program
{
	public static void Main(string[] args)
	{
		var mode = args[0];
		if (mode == "cs")
			CsAstGen.Run(args[1]);
		if (mode == "ts")
			TsAstGen.Run(args[1]);
		if (mode == "dart")
			DartAstGen.Run(args[1]);

	}
}
