using System;

namespace SetAssociativeCache
{
    public interface ISetAssociativeCache<TKey, TValue>
    {
        TValue Get(TKey key, Func<TKey, TValue> onMiss);
        void Put(TKey key, TValue val);
    }
}