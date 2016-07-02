﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICSharpCode.Decompiler
{
	static class CollectionExtensions
	{
		public static HashSet<T> ToHashSet<T>(this IEnumerable<T> input)
		{
			return new HashSet<T>(input);
		}
		
		public static T PopOrDefault<T>(this Stack<T> stack)
		{
			if (stack.Count == 0)
				return default(T);
			return stack.Pop();
		}
		
		public static T PeekOrDefault<T>(this Stack<T> stack)
		{
			if (stack.Count == 0)
				return default(T);
			return stack.Peek();
		}
		
		public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> input)
		{
			foreach (T item in input)
				collection.Add(item);
		}
		
		/// <summary>
		/// Equivalent to <code>collection.Select(func).ToArray()</code>, but more efficient as it makes
		/// use of the input collection's known size.
		/// </summary>
		public static U[] SelectArray<T, U>(this ICollection<T> collection, Func<T, U> func)
		{
			U[] result = new U[collection.Count];
			int index = 0;
			foreach (var element in collection) {
				result[index++] = func(element);
			}
			return result;
		}
		
		/// <summary>
		/// The merge step of merge sort.
		/// </summary>
		public static IEnumerable<T> Merge<T>(this IEnumerable<T> input1, IEnumerable<T> input2, Comparison<T> comparison)
		{
			var enumA = input1.GetEnumerator();
			var enumB = input2.GetEnumerator();
			bool moreA = enumA.MoveNext();
			bool moreB = enumB.MoveNext();
			while (moreA && moreB) {
				if (comparison(enumA.Current, enumB.Current) <= 0) {
					yield return enumA.Current;
					moreA = enumA.MoveNext();
				} else {
					yield return enumB.Current;
					moreB = enumB.MoveNext();
				}
			}
			while (moreA) {
				yield return enumA.Current;
				moreA = enumA.MoveNext();
			}
			while (moreB) {
				yield return enumB.Current;
				moreB = enumB.MoveNext();
			}
		}
	}
}