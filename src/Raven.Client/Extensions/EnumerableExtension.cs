using System;
using System.Collections.Generic;
using System.Linq;

namespace Raven.Client.Extensions
{
    internal static class EnumerableExtension
    {
        public static void ApplyIfNotNull<T>(this IEnumerable<T> self, Action<T> action)
        {
            if (self == null)
                return;
            foreach (var item in self)
            {
                action(item);
            }
        }

        public static bool ContentEquals<TValue>(IEnumerable<TValue> x, IEnumerable<TValue> y)
        {
            if (x == null || y == null)
                return x == null && y == null;

            return x.SequenceEqual(y);
        }

        public static IEnumerable<T> EmptyIfNull<T>(this IEnumerable<T> self)
        {
            if (self == null)
                return new T[0];
            return self;
        }

        public static int GetEnumerableHashCode<TKey>(this IEnumerable<TKey> self)
        {
            int result = 0;

            foreach (var kvp in self)
            {
                result = (result * 397) ^ kvp.GetHashCode();
            }

            return result;
        }

        public static IEnumerable<T> ForceEnumerate<T>(this ICollection<T> collection)
        {
            // thanks to: https://stackoverflow.com/questions/47630824/is-c-sharp-linq-orderby-threadsafe-when-used-with-concurrentdictionarytkey-tva#
            foreach (var item in collection)
                yield return item;
        }
    }
}
