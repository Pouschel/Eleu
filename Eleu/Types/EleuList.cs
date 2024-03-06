using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eleu.Types;

internal class EleuList
{
  List<object> data = [];

  public EleuList()
  {
  }

  internal void Add(object value) => data.Add(value);
}
