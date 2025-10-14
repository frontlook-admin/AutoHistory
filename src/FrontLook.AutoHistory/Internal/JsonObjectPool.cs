// Copyright (c) Arch team. All rights reserved.

using System;
using System.Collections.Concurrent;
using Newtonsoft.Json.Linq;

namespace FrontLook.IAutoHistory.Internal
{
    /// <summary>
    /// Provides object pooling for JObject instances to reduce garbage collection pressure.
    /// </summary>
    internal static class JsonObjectPool
    {
        // Maximum pool size to prevent excessive memory usage
        private const int MaxPoolSize = 128;
        
        // Thread-safe object pools for JObject instances
        private static readonly ConcurrentBag<JObject> _objectPool = new ConcurrentBag<JObject>();
        private static readonly ConcurrentBag<JObject> _beforeAfterPool = new ConcurrentBag<JObject>();
        
        // Statistics for monitoring (optional)
        private static int _objectsCreated;
        private static int _objectsReused;

        /// <summary>
        /// Gets a JObject from the pool or creates a new one if the pool is empty.
        /// </summary>
        public static JObject GetObject()
        {
            if (_objectPool.TryTake(out var obj))
            {
                System.Threading.Interlocked.Increment(ref _objectsReused);
                return obj;
            }
            
            System.Threading.Interlocked.Increment(ref _objectsCreated);
            return new JObject();
        }

        /// <summary>
        /// Gets a pair of before/after JObjects for tracking property changes.
        /// </summary>
        public static (JObject Before, JObject After) GetBeforeAfterPair()
        {
            // Removing unused variables that were causing build errors
            
            if (_beforeAfterPool.TryTake(out var obj1) && _beforeAfterPool.TryTake(out var obj2))
            {
                System.Threading.Interlocked.Increment(ref _objectsReused);
                System.Threading.Interlocked.Increment(ref _objectsReused);
                return (obj1, obj2);
            }
            
            System.Threading.Interlocked.Increment(ref _objectsCreated);
            System.Threading.Interlocked.Increment(ref _objectsCreated);
            return (new JObject(), new JObject());
        }

        /// <summary>
        /// Returns a JObject to the pool after use.
        /// </summary>
        public static void ReturnObject(JObject obj)
        {
            if (obj == null) return;
            
            // Clear all properties to prevent memory leaks
            obj.RemoveAll();
            
            // Only add to the pool if we haven't exceeded the maximum size
            if (_objectPool.Count < MaxPoolSize)
            {
                _objectPool.Add(obj);
            }
        }

        /// <summary>
        /// Returns a pair of before/after JObjects to the pool after use.
        /// </summary>
        public static void ReturnBeforeAfterPair(JObject before, JObject after)
        {
            if (before != null)
            {
                before.RemoveAll();
                if (_beforeAfterPool.Count < MaxPoolSize)
                {
                    _beforeAfterPool.Add(before);
                }
            }
            
            if (after != null)
            {
                after.RemoveAll();
                if (_beforeAfterPool.Count < MaxPoolSize)
                {
                    _beforeAfterPool.Add(after);
                }
            }
        }

        /// <summary>
        /// Gets statistics about the object pool usage.
        /// </summary>
        public static (int Created, int Reused, int PoolSize) GetStatistics()
        {
            return (_objectsCreated, _objectsReused, _objectPool.Count + _beforeAfterPool.Count);
        }
    }
}