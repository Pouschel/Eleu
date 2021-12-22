
using System.Diagnostics;
using System.Globalization;

class Program
{
	int nTests, nSuccess, nFail, nSkipped;

	Dictionary<string, TimeSpan> benchMarkResults = new();

	void TestFile(string fileName)
	{
		string? msg = null;
		try
		{

			Interlocked.Increment(ref nTests);
			var source = File.ReadAllText(fileName);
			var sw = new StringWriter();
			Globals.RunTestCode(source, sw);
			var res = sw.ToString().ReplaceLineEndings();
			var expected = GetSourceOutput(source).ReplaceLineEndings();
			if (res == expected)
			{
				Interlocked.Increment(ref nSuccess);
				return;
			}
			msg = $@"..........got.......
{res}
.......expected.........
{expected}";

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

	void BenchmarkFile(string fileName)
	{
		Interlocked.Increment(ref nTests);
		var source = File.ReadAllText(fileName);
		Stopwatch watch = Stopwatch.StartNew();
		Globals.RunTestCode(source, TextWriter.Null);
		var elapsed = watch.Elapsed;
		lock (benchMarkResults)
		{
			benchMarkResults.Add(fileName, elapsed);
		}
	}

	static string GetSourceOutput(string source)
	{
		const string search = "// expect: ";
		const string searchRtErr = "// expect runtime error: ";

		var sw = new StringWriter();
		var lines = source.Split('\n');
		foreach (var line in lines)
		{
			int idx = line.IndexOf(search);
			if (idx >= 0)
			{
				var resString = line[(idx + search.Length)..].TrimEnd();
				sw.WriteLine(resString);
			}
			idx = line.IndexOf(searchRtErr);
			if (idx >= 0)
			{
				sw.WriteLine(line.Substring(idx + searchRtErr.Length).TrimEnd());
			}
			idx = line.IndexOf("//");
			if (idx < 0) continue;
			idx = line.IndexOf("Error at", idx);
			if (idx < 0) continue;
			idx = line.IndexOf(": ", idx);
			if (idx < 0) continue;
			sw.WriteLine(line.Substring(idx + 2).TrimEnd());
		}
		return sw.ToString();
	}

	void RunActionInDir(string dir, Action<string> action)
	{
		var files = Directory.GetFiles(dir, "*.lox");
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
		lock (this) Console.Write($"\r{file}           ");
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

		var files = Directory.GetFiles(dir, "*.lox");
		Parallel.ForEach(files, file =>
		{
			if (IsIgnored(file)) return;
			action(file);
		});
	}

	void RunTests(string dir)
	{
		Console.WriteLine($"Start Testing dir: {dir}");
		var watch = Stopwatch.StartNew();
		RunActionInDir(dir, TestFile);

		Console.WriteLine();
		Console.WriteLine("---- Test Results ---");
		Console.WriteLine($"Skipped: {nSkipped,4}");
		Console.WriteLine($"Tests  : {nTests,4} in {watch.ElapsedMilliseconds} ms");
		Console.WriteLine($"Success: {nSuccess,4}");
		if (nFail > 0)
			Console.ForegroundColor = ConsoleColor.Red;
		Console.WriteLine($"Fail   : {nFail,4}");
	}


	void RunBenchmarks(string dir)
	{
		var watch = Stopwatch.StartNew();
		RunActionInDir(dir, BenchmarkFile);
		var total = watch.Elapsed.TotalSeconds;
		Console.WriteLine();
		Console.WriteLine("---- Benchmark Results ---");
		Console.WriteLine($"Skipped   : {nSkipped,4}");
		Console.WriteLine($"Benchmarks: {nTests,4} in {watch.Elapsed.TotalSeconds:f2} s");
		List<string> lines = new();
		lines.Add($"--- {DateTime.Now} ---");
		foreach (var kv in 		benchMarkResults.OrderBy(kv => Path.GetFileNameWithoutExtension(kv.Key)))
		{
			lines.Add($"{Path.GetFileNameWithoutExtension(kv.Key),-18}: {kv.Value.TotalMilliseconds,6:###,###} ms");
		}
		lines.Add($"            Total : {total,6:##,###.#} s");
		File.AppendAllLines(Path.Combine(dir, "_Results.txt"),lines);
	}

	public static void Main(string[] args)
	{
		Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
		Console.WriteLine("CsLox Tester v1");
		Console.WriteLine(
@"Arguments: 
-test TestDir [FileToExecute]
-benchmark BenchmarkDir");
		if (args.Length < 2) return;
		switch (args[0])
		{
			case "-test":
				{
					var prog = new Program();
					prog.RunTests(args[1]);
					if (args.Length >= 3 && prog.nFail == 0)
					{
						Console.WriteLine($"Running file: {args[2]}");
						Globals.RunFile(args[2], Console.Out, true);
					}
					break;
				}
			case "-benchmark":
				{
					var prog = new Program();
					prog.RunBenchmarks(args[1]);
					break;
				}

		}
		if (Debugger.IsAttached) Console.ReadLine();
	}
}

