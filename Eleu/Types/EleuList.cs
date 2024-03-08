using System.Collections;

namespace Eleu.Types;

internal class EleuList: IEnumerable<object>
{
  List<object> data = [];

  public EleuList()
  {
  }
  public int Len => data.Count;
  public IEnumerator<object> GetEnumerator() => data.GetEnumerator();
  internal void Add(object value) => data.Add(value);
  IEnumerator IEnumerable.GetEnumerator() => data.GetEnumerator();

}
