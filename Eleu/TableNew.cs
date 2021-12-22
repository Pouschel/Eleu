global using static CsLox.TableStatics;

namespace CsLox;


struct Entry
{
	public string? key;
	public Value value;
}


class Table
{
	const double TABLE_MAX_LOAD = 0.75;

	public int count;
	public int capacity;
	public Entry[] entries;

	public Table()
	{
		this.capacity = 10;
		entries = new Entry[this.capacity];
	}

	public bool Set(string key, in Value val)
	{
		if (count + 1 > capacity * TABLE_MAX_LOAD)
		{
			adjustCapacity(capacity * 2);
		}

		ref Entry entry = ref findEntry(entries, key);
		bool isNewKey = entry.key == null;
		if (isNewKey && IS_NIL(entry.value)) count++;
		entry.key = key;
		entry.value = val;
		return isNewKey;
	}

	static ref Entry findEntry(Entry[] entries, string key)
	{
		int index = key.GetHashCode();
		if (index < 0) index = -index;
		int capacity = entries.Length;
		int tomestoneIndex = -1;
		for (; ; index++)
		{
			index %= capacity;
			ref Entry entry = ref entries[index];
			if (entry.key == null)
			{
				if (IS_NIL(entry.value))
				{
					if (tomestoneIndex < 0) return ref entry;
					return ref entries[tomestoneIndex];
				}
				else
				{
					// We found a tombstone.
					if (tomestoneIndex < 0) tomestoneIndex = index;
				}
			}
			else if (entry.key == key)
			{
				// We found the key.
				return ref entry;
			}
		}
	}

	void adjustCapacity(int capacity)
	{
		Entry[] entries = new Entry[capacity];
		this.count = 0;
		for (int i = 0; i < this.capacity; i++)
		{
			ref Entry entry = ref this.entries[i];
			if (entry.key == null) continue;

			ref Entry dest = ref findEntry(entries, entry.key);
			this.count++;
			dest.key = entry.key;
			dest.value = entry.value;
		}

		this.entries = entries;
		this.capacity = capacity;
	}

	public bool Get(string key, out Value value)
	{
		value = NIL_VAL;
		if (count == 0) return false;
		ref Entry entry = ref findEntry(entries, key);
		if (entry.key == null) return false;
		value = entry.value;
		return true;
	}

	public bool Remove(string key)
	{
		if (count == 0) return false;

		// Find the entry.
		ref Entry entry = ref findEntry(entries, key);
		if (entry.key == null) return false;

		// Place a tombstone in the entry.
		entry.key = null;
		entry.value = BOOL_TRUE;
		return true;
	}

}



static class TableStatics
{
	public static bool tableSet(Table table, string key, Value value) => table.Set(key, value);

	public static void tableAddAll(Table from, Table to)
	{
		for (int i = 0; i < from.capacity; i++)
		{
			ref Entry entry = ref from.entries[i];
			if (entry.key != null)
			{
				tableSet(to, entry.key, entry.value);
			}
		}
	}

	public static bool tableGet(Table table, string key, out Value value)
		=> table.Get(key, out value);

	public static bool tableDelete(Table table, string key)
		=> table.Remove(key);

}
