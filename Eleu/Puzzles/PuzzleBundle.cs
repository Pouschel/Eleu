
namespace Eleu.Puzzles;

public class PuzzleBundle
{
	List<Puzzle> puzzles = new();
	public string Code { get; init; }
	public string Name => puzzles.Count > 0 ? puzzles[0].Name : "";
	internal PuzzleBundle(string code)
	{
		this.Code = code;
	}
	public void AddPuzzle(Puzzle puzzle)
	{
		puzzle.BundleIndex = puzzles.Count;
		puzzles.Add(puzzle);
	}

	public int Count => puzzles.Count;

	internal void SetImageNameHints(string eleuName)
	{
		if (puzzles.Count == 0) return;
		var fn = Path.ChangeExtension(eleuName,".png");
		puzzles[0].ImageNameHint = fn;
		for (int i = 1; i < puzzles.Count; i++)
		{
			var puz = puzzles[i];
			puz.ImageNameHint = fn + $"_{i + 1}.png";
		}
	}
	public Puzzle this[int index] => puzzles[index];


}
