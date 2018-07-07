namespace SetAssociativeCache
{
    public struct CacheLine<TKey, TValue>
    {
        public TKey Key;
        public TValue Val;
        public bool Valid;
        public int HitCount;
        public long LastAccessTicks;
    }
}