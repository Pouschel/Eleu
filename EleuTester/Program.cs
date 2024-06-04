using System.Diagnostics;
using System.Text;
using Eleu;

class Program
{
	int nTests, nSuccess, nFail, nSkipped;

	Dictionary<string, TimeSpan> benchMarkResults = new();

	int testDirLen;
	bool useVm;
	void TestFile(string fileName)
	{
		string? msg = null;
		try
		{
			Interlocked.Increment(ref nTests);
			var source = File.ReadAllText(fileName);
			var sw = new StringWriter();
			RunTestCode(fileName, source, sw);
			var res = ProcessScriptOutput(sw.ToString()).ReplaceLineEndings().TrimEnd();
			var expected = GetSourceOutput(source).ReplaceLineEndings().TrimEnd();
			if (res == expected)
			{
				Interlocked.Increment(ref nSuccess);
				return;
			}
			//if (expected.Length==0)
			//{
			//	Interlocked.Increment(ref nSkipped);
			//	return;
			//}
			msg = $@"..........got.......
{res}
.......expected.........
{expected}";

		}
		catch (EleuAssertionFail aex)
		{
			var location = aex.Status.HasValue ? aex.Status.Value.Message : "";
			msg = $"{location}: {aex.Message}";
		}
		catch (Exception ex)
		{
			msg = ex.ToString();
		}
		if (msg == null) return;
		Interlocked.Increment(ref nFail);
		lock (this)
		{
			var col = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.Magenta;
			Console.WriteLine($"\r{fileName}              ");
			Console.ForegroundColor = col;
			Console.WriteLine(msg);
		}
	}

	string ProcessScriptOutput(string s)
	{
		var lines = s.Split('\n');
		var sw = new StringWriter();
		foreach (var line in lines)
		{
			if (line.StartsWith("Die _puzzle() Funktion überschreibt das aktive Puzzle"))
				continue;
			var l = line.TrimEnd();
			int idx = l.IndexOf("): ");
			if (idx >= 0)
				l = l[(idx + 3)..];
			sw.WriteLine(l);
		}
		return sw.ToString();
	}
	void BenchmarkFile(string fileName)
	{
		Interlocked.Increment(ref nTests);
		var source = File.ReadAllText(fileName);
		var opt = new EleuOptions();
		Stopwatch watch = Stopwatch.StartNew();
		Globals.CompileAndRunAst(source, fileName, opt, useVm);
		var elapsed = watch.Elapsed;
		lock (benchMarkResults)
		{
			benchMarkResults.Add(fileName, elapsed);
		}
	}

	public bool RunTestCode(string path, string source, TextWriter tw)
	{
		var opt = new EleuOptions()
		{
			Out = tw,
			Err = tw,
			DumpStackOnError = false,
			UseDebugger = true,
			ThrowOnAssert = true,
		};
		var cres = Globals.CompileAndRunAst(source, path, opt, useVm);
		return cres == EEleuResult.Ok;
	}

	static string GetSourceOutput(string source)
	{

		string[] searches = new string[] { "// expect: ", "//? ", "// expect runtime error: ", "//Rerr: ", "//Cerr: " };

		var sw = new StringWriter();
		var lines = source.Split('\n');
		foreach (var line in lines)
		{
			for (int i = 0; i < searches.Length; i++)
			{
				string? search = searches[i];
				int idx = line.IndexOf(search);
				if (idx < 0) continue;
				if (idx >= 0)
				{
					int start = idx + (i < 4 ? search.Length : 2);
					var resString = line[start..].TrimEnd();
					sw.WriteLine(resString);
					break;
				}
			}
		}
		return sw.ToString();
	}

	void RunActionInDir(string dir, Action<string> action)
	{
		string locDir = Path.GetFileName(dir);
		if (locDir[0] == '-')
			return;
		var files = Directory.GetFiles(dir, "*.eleu");
		foreach (var file in files)
		{
			if (IsIgnored(file)) continue;
			action(file);
		}
		foreach (var idir in Directory.GetDirectories(dir))
		{
			RunActionInDir(idir, action);
		}
	}

	bool IsIgnored(string file)
	{
		lock (this) Console.Write($"\r{file[testDirLen..]}           ");
		var baseName = Path.GetFileName(file);
		if (baseName[0] == '-')
		{
			Interlocked.Increment(ref nSkipped);
			return true;
		}
		return false;
	}
	void RunActionInDirParallel(string dir, Action<string> action)
	{
		Parallel.ForEach(Directory.GetDirectories(dir), idir =>
			RunActionInDirParallel(idir, action));

		var files = Directory.GetFiles(dir, "*.eleu");
		Parallel.ForEach(files, file =>
		{
			if (IsIgnored(file)) return;
			action(file);
		});
	}

	void RunTests(string dir)
	{
		var fcol = Console.ForegroundColor;
		testDirLen = dir.Length;
		Console.WriteLine($"Start Testing dir: {dir}");
		var watch = Stopwatch.StartNew();
		RunActionInDirParallel(dir, TestFile);
		//RunActionInDir(dir, TestFile);

		Console.WriteLine();
		Console.WriteLine("---- Test Results ---");
		Console.WriteLine($"Skipped: {nSkipped,4}");
		Console.WriteLine($"Tests  : {nTests,4} in {watch.ElapsedMilliseconds} ms");
		Console.WriteLine($"Success: {nSuccess,4}");
		if (nFail > 0)
			Console.ForegroundColor = ConsoleColor.Red;
		Console.WriteLine($"Fail   : {nFail,4}");
		Console.ForegroundColor = fcol;
	}


	void RunBenchmarks(string dir)
	{
		testDirLen = dir.Length;
		var watch = Stopwatch.StartNew();
		RunActionInDir(dir, BenchmarkFile);
		var total = watch.Elapsed.TotalSeconds;
		Console.WriteLine();
		Console.WriteLine("---- Benchmark Results ---");
		Console.WriteLine($"Skipped   : {nSkipped,4}");
		Console.WriteLine($"Benchmarks: {nTests,4} in {watch.Elapsed.TotalSeconds:f2} s");
		List<string> lines = new();
		lines.Add($"--- {DateTime.Now} ---");
		foreach (var kv in benchMarkResults.OrderBy(kv => Path.GetFileNameWithoutExtension(kv.Key)))
		{
			lines.Add($"{Path.GetFileNameWithoutExtension(kv.Key),-18}: {kv.Value.TotalMilliseconds,6:###,###} ms");
		}
		lines.Add($"            Total : {total,6:##,###.##} s");
		File.AppendAllLines(Path.Combine(dir, "_Results.txt"), lines);
	}

	public static void Main(string[] args)
	{
		//CliTest.Test(); return;
		Console.WriteLine("Eleu Tester v2");
		Console.WriteLine(
@"Arguments: 
-vm                        use vm
-test TestDir [FileToExecute] 
-benchmark BenchmarkDir
-dump file         Dump code execution to log file:");

		if (args.Length < 1) return;
		var prog = new Program();
		string file = "", dumpFile="";
		for (int i = 0; i < args.Length; i++)
		{
			var arg = args[i];
			switch (arg)
			{
				case "-vm": prog.useVm = true; continue;
				case "-test":
					{
						prog.RunTests(args[++i]);
						continue;
					}
				case "-benchmark":
					{
						prog.RunBenchmarks(args[++i]);
						continue;
					}
				case "-dump":
					dumpFile = args[++i];
					break;
        default: file = arg; break;
			}
		}
		if (!string.IsNullOrEmpty(file))
		{
			Console.WriteLine($"Running file: {file}");
			Globals.RunFile(file, Console.Out, prog.useVm, dumpFile);
		}
		Console.WriteLine("Finished!");
		//Console.ReadLine();
	}
}



