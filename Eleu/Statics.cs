
namespace Eleu;

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
