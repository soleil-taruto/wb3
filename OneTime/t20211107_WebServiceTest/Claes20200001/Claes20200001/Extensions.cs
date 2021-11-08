using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Charlotte
{
	public static class Extensions
	{
		public static IEnumerable<T> ForEach<T>(this IEnumerable<T> src, Action<T> action)
		{
			foreach (T element in src)
				action(element);

			return src;
		}

		public static IEnumerable<T> OrderBy<T>(this IEnumerable<T> src, Comparison<T> comp)
		{
			List<T> list = src.ToList();
			list.Sort(comp);
			return list;
		}

		public static IEnumerable<T> OrderedDistinct<T>(this IEnumerable<T> src, Func<T, T, bool> match)
		{
			IEnumerator<T> reader = src.GetEnumerator();

			if (reader.MoveNext())
			{
				T last = reader.Current;

				yield return last;

				while (reader.MoveNext())
				{
					if (!match(reader.Current, last))
					{
						last = reader.Current;
						yield return last;
					}
				}
			}
		}

		public static IEnumerable<T> DistinctOrderBy<T>(this IEnumerable<T> src, Comparison<T> comp)
		{
			return src.OrderBy(comp).OrderedDistinct((a, b) => comp(a, b) == 0);
		}
	}
}
