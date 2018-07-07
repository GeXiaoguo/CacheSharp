using System;
using System.Collections.Generic;
using System.Diagnostics;
using SetAssociativeCache;

namespace Cache
{
    public class SetSetAssociativeCache<TKey, TValue> : ISetAssociativeCache<TKey, TValue>
    {
        private readonly Dictionary<int, CacheLine<TKey, TValue>[]> _cacheBlocks = new Dictionary<int, CacheLine<TKey, TValue>[]>();
        private readonly int _maxNumOfBlocks;
        private readonly int _blockSize;
        private readonly Func<CacheLine<TKey, TValue>[], int> _evictionFunc;
        private readonly Stopwatch _stopwatch = new Stopwatch();

        public SetSetAssociativeCache(int maxSize, int blockSize, EvictionStragety evictionStragety, Func<CacheLine<TKey, TValue>[], int> customizedEvictionFunc = null)
        {
            if (maxSize % blockSize != 0)
            {
                throw new ArgumentException($"{nameof(maxSize)} has to be multiples of {nameof(blockSize)}");
            }

            _maxNumOfBlocks = maxSize / blockSize;
            _blockSize = blockSize;
            switch (evictionStragety)
            {
                case EvictionStragety.LRU:
                    _evictionFunc = block => EvictionFuncs.Evict(block, EvictionStragety.LRU);
                    break;
                case EvictionStragety.MRU:
                    _evictionFunc = block => EvictionFuncs.Evict(block, EvictionStragety.MRU);
                    break;
                case EvictionStragety.Customized:
                    _evictionFunc = customizedEvictionFunc ?? throw new ArgumentException($"{nameof(customizedEvictionFunc)} can not be null");
                    break;
                default:
                    throw new NotImplementedException($"{nameof(EvictionStragety)} {evictionStragety} is not implemented yet");
            }

            _stopwatch.Start();
        }

        public TValue Get(TKey key, Func<TKey, TValue> onMiss)
        {
            var block = GetOrAddBlock(key);
            int hitIndex = GetHitIndex(block, key);
            if (hitIndex >= 0)
            {
                block[hitIndex].HitCount++;
                block[hitIndex].LastAccessTicks = _stopwatch.ElapsedTicks;
                return block[hitIndex].Val;
            }

            var val = onMiss == null ? default(TValue) : onMiss(key);
            Replace(block, key, val, _evictionFunc);
            return val;
        }

        public void Put(TKey key, TValue val)
        {
            var block = GetOrAddBlock(key);
            Replace(block, key, val, _evictionFunc);
        }

        private CacheLine<TKey, TValue>[] GetOrAddBlock(TKey key)
        {
            int blockKey = BitMix(key.GetHashCode()) % _maxNumOfBlocks;
            if (_cacheBlocks.TryGetValue(blockKey, out var block))
            {
                return block;
            }

            _cacheBlocks.Add(blockKey, new CacheLine<TKey, TValue>[_blockSize]);
            return _cacheBlocks[blockKey];
        }

        private void Replace(CacheLine<TKey, TValue>[] block, TKey key, TValue val, Func<CacheLine<TKey, TValue>[], int> evictionElection)
        {
            int evictIndex = evictionElection(block);
            block[evictIndex].Key = key;
            block[evictIndex].Val = val;
            block[evictIndex].HitCount = 1;
            block[evictIndex].LastAccessTicks = _stopwatch.ElapsedTicks;
        }

        private static int GetHitIndex(CacheLine<TKey, TValue>[] lines, TKey key)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].HitCount > 0 && EqualityComparer<TKey>.Default.Equals(key, lines[i].Key))
                {
                    return i;
                }
            }

            return -1;
        }

        private static int BitMix(int x)
        {
            x = ((x >> 16) ^ x) * 0x45d9f3b;
            x = ((x >> 16) ^ x) * 0x45d9f3b;
            x = (x >> 16) ^ x;
            return x;
        }
    }

    public static class EvictionFuncs
    {
        private static bool IsMoreRecent(long thisTicks, long thatTicks)
        {
            return thisTicks > thatTicks;
        }

        private static bool IsLessRecent(long thisTicks, long thatTicks)
        {
            return thisTicks < thatTicks;
        }

        public static int Evict<TKey, TVal>(CacheLine<TKey, TVal>[] lines, EvictionStragety evictionStragety)
        {
            if (lines[0].HitCount == 0)
            {
                return 0;
            }

            int evictionIndex = 0;
            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].HitCount == 0)
                {
                    return i;
                }

                if (evictionStragety == EvictionStragety.LRU && IsLessRecent(lines[i].LastAccessTicks, lines[evictionIndex].LastAccessTicks))
                {
                    evictionIndex = i;
                }
                else if (evictionStragety == EvictionStragety.MRU && IsMoreRecent(lines[i].LastAccessTicks, lines[evictionIndex].LastAccessTicks))
                {
                    evictionIndex = i;
                }
            }

            return evictionIndex;
        }
    }
}