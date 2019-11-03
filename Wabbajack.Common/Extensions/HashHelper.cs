using System;
using System.Collections.Generic;

/*
 * Taken from: http://stackoverflow.com/questions/263400/what-is-the-best-algorithm-for-an-overridden-system-object-gethashcode
 */
namespace Wabbajack
{
    public static class HashHelper
    {
        public static int GetHashCode<T1, T2>(T1 arg1, T2 arg2)
        {
            unchecked
            {
                return 31 * (arg1 == null ? 0 : arg1.GetHashCode())
                    + (arg2 == null ? 0 : arg2.GetHashCode());
            }
        }

        public static int GetHashCode<T1, T2, T3>(T1 arg1, T2 arg2, T3 arg3)
        {
            unchecked
            {
                int hash = (arg1 == null ? 0 : arg1.GetHashCode());
                hash = 31 * hash + (arg2 == null ? 0 : arg2.GetHashCode());
                return 31 * hash + (arg3 == null ? 0 : arg3.GetHashCode());
            }
        }

        public static int GetHashCode<T1, T2, T3, T4>(T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            unchecked
            {
                int hash = (arg1 == null ? 0 : arg1.GetHashCode());
                hash = 31 * hash + (arg2 == null ? 0 : arg2.GetHashCode());
                hash = 31 * hash + (arg3 == null ? 0 : arg3.GetHashCode());
                return 31 * hash + (arg4 == null ? 0 : arg4.GetHashCode());
            }
        }

        public static int GetHashCode<T>(params T[] list)
        {
            unchecked
            {
                int hash = 0;
                if (list == null) return hash;
                for (int i = 0; i < list.Length; i++)
                {
                    hash = 31 * hash + GetHashCode(list[i]);
                }
                return hash;
            }
        }

        public static int GetHashCode<T>(ReadOnlySpan<T> span)
        {
            unchecked
            {
                int hash = 0;
                if (span == null) return hash;
                for (int i = 0; i < span.Length; i++)
                {
                    hash = 31 * hash + GetHashCode(span[i]);
                }
                return hash;
            }
        }

        public static int GetHashCode<T>(T t)
        {
            unchecked
            {
                return (t == null ? 0 : t.GetHashCode());
            }
        }

        public static int GetHashCode<T>(IEnumerable<T> list)
        {
            unchecked
            {
                int hash = 0;
                foreach (var item in list)
                {
                    hash = 31 * hash + (item == null ? 0 : item.GetHashCode());
                }
                return hash;
            }
        }

        /// <summary>
        /// Gets a hashcode for a collection for that the order of items 
        /// does not matter.
        /// So {1, 2, 3} and {3, 2, 1} will get same hash code.
        /// </summary>
        public static int GetHashCode_OrderBlind<T>(
            IEnumerable<T> list)
        {
            unchecked
            {
                int hash = 0;
                int count = 0;
                foreach (var item in list)
                {
                    hash += (item == null ? 0 : item.GetHashCode());
                    count++;
                }
                return 31 * hash + count.GetHashCode();
            }
        }

        /// <summary>
        /// Alternative way to get a hashcode is to use a fluent 
        /// interface like this:<br />
        /// return 0.CombineHashCode(field1).CombineHashCode(field2).
        ///     CombineHashCode(field3);
        /// </summary>
        public static int CombineHashCode<T>(this int hashCode, T arg)
        {
            unchecked
            {
                return CombineHashCode(hashCode, (arg == null ? 0 : arg.GetHashCode()));
            }
        }

        public static int CombineHashCode(this int hashCode, int rhsHash)
        {
            unchecked
            {
                return 31 * hashCode + rhsHash;
            }
        }
    }
}
