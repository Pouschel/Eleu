global using static CsLox.TableStatics;

namespace CsLox;

internal class Table: Dictionary<string,Value>
{
	public Table()
	{
	}
}

static class TableStatics
{
	public static bool tableSet(Table table, string key, Value value)
	{
		int count = table.Count;
		table[key] = value;
		return count != table.Count;
	}

	public static void tableAddAll(Table from, Table to)
	{
		foreach (var item in from)
		{
			to[item.Key] = item.Value;
		}
	}

	public static bool tableGet(Table table, string key, out Value value) 
		=> table.TryGetValue(key, out value);

	public static bool tableDelete(Table table, string key) 
		=> table.Remove(key);

}
