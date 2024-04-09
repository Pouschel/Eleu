using System.Collections;

namespace Eleu.Types;

internal class EleuList : IEnumerable<object>
{
  readonly List<object> data = [];

  public EleuList()
  {
  }  
  public EleuList(params object[] data): this()
  {
    this.data.AddRange(data);
  }
  public int Len => data.Count;
  public IEnumerator<object> GetEnumerator() => data.GetEnumerator();
  internal void Add(object value) => data.Add(value);
  IEnumerator IEnumerable.GetEnumerator() => data.GetEnumerator();

  public object this[int index] { get => data[index]; set => data[index] = value; }
}
