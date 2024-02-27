﻿using System.Collections.Concurrent;

namespace Imazen.Common.Instrumentation.Support
{
    class DictionaryCounter<T> where T : notnull
    {
        readonly ConcurrentDictionary<T, Counter> dict;
        readonly Counter count;
        readonly Counter otherCount;

        public int MaxKeyCount { get; }
        public T LimitExceededKey { get; }

        public DictionaryCounter(T updateFailedKey) : this(int.MaxValue, updateFailedKey, EqualityComparer<T>.Default)
        { }

        public DictionaryCounter(int maxKeyCount, T limitExceededKey) : this(maxKeyCount, limitExceededKey, EqualityComparer<T>.Default)
        { }
        public DictionaryCounter(int maxKeyCount, T limitExceededKey, IEqualityComparer<T> comparer) :
            this(maxKeyCount, limitExceededKey, Enumerable.Empty<KeyValuePair<T, long>>(), comparer)
        { }
        
        public DictionaryCounter(int maxKeyCount, T limitExceededKey, IEnumerable<KeyValuePair<T, long>> initial, IEqualityComparer<T> comparer)
        {
            MaxKeyCount = maxKeyCount;
            LimitExceededKey = limitExceededKey;
            otherCount = new Counter(0);
            var initialPairs = initial
                .Select(pair => new KeyValuePair<T, Counter>(pair.Key, new Counter(pair.Value)))
                .Take(maxKeyCount - 1)
                .Concat(new[] { new KeyValuePair<T, Counter>(LimitExceededKey, otherCount) });
          
            dict = new ConcurrentDictionary<T, Counter>(initialPairs, comparer);
            count = new Counter(dict.Count);
        }

        public bool TryRead(T key, out long v)
        {
            if (dict.TryGetValue(key, out var c))
            {
                v = c.Value;
                return true;
            }
            else
            {
                v = 0;
                return false;
            }
        }

        public bool Contains(T key)
        {
            return dict.ContainsKey(key);
        }

        Counter GetOrAddInternal(T key, long initialValue, bool applyLimitSwap)
        {
            for (var retryCount = 0; retryCount < 10; retryCount++) {
                if (dict.TryGetValue(key, out var result))
                {
                    return result;
                } else
                {
                    if (!applyLimitSwap)
                    {
                        count.Increment();
                        var newValue = new Counter(initialValue);
                        result = dict.GetOrAdd(key, newValue);
                        if (result != newValue)
                        {
                            count.Decrement();
                        }
                        return result;
                    }
                    else
                    {
                        var existingSize = count.Value;
                        if (existingSize < MaxKeyCount)
                        {
                            if (count.IncrementIfMatches(existingSize))
                            {
                                var newValue = new Counter(initialValue);
                                result = dict.GetOrAdd(key, newValue);
                                if (result != newValue)
                                {
                                    count.Decrement();
                                }
                                return result;
                            }else
                            {
                                continue; //Let's retry
                            }
                        }else
                        {
                            return otherCount;
                        }
                    }
                }
            }
            
            // When 
            return otherCount;
        }

       

        /// <summary>
        /// Creates the entry if it doesn't already exist
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public long Increment(T key)
        {
            return GetOrAddInternal(key, 0, MaxKeyCount != int.MaxValue).Increment();
        }


        public IEnumerable<KeyValuePair<T,long>> GetCounts()
        {
            return dict.Select(pair => new KeyValuePair<T, long>(pair.Key, pair.Value.Value));
        }
    }
}
