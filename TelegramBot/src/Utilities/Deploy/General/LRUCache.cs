namespace TelegramBot.Utilities.Deploy.General
{
    public class LRUCache<TKey, TValue>
    {
        private readonly int _capacity;
        private readonly Dictionary<TKey, LinkedListNode<LRUCacheItem<TKey, TValue>>> _cacheMap = new Dictionary<TKey, LinkedListNode<LRUCacheItem<TKey, TValue>>>();
        private readonly LinkedList<LRUCacheItem<TKey, TValue>> _lruList = new LinkedList<LRUCacheItem<TKey, TValue>>();
        private readonly TimeSpan _itemTimeout;
        private readonly Timer _cleanupTimer;

        public LRUCache(int capacity, TimeSpan itemTimeout)
        {
            _capacity = capacity;
            _itemTimeout = itemTimeout;
            _cleanupTimer = new Timer(CleanupExpiredItems, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        public void Add(TKey key, TValue value)
        {
            if (_cacheMap.TryGetValue(key, out LinkedListNode<LRUCacheItem<TKey, TValue>> existingNode))
            {
                _lruList.Remove(existingNode);
            }
            else if (_cacheMap.Count >= _capacity)
            {
                RemoveFirst();
            }

            LRUCacheItem<TKey, TValue> cacheItem = new LRUCacheItem<TKey, TValue>(key, value, DateTime.UtcNow.Add(_itemTimeout));
            LinkedListNode<LRUCacheItem<TKey, TValue>> node = new LinkedListNode<LRUCacheItem<TKey, TValue>>(cacheItem);
            _lruList.AddLast(node);
            _cacheMap[key] = node;
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            value = default(TValue);
            if (_cacheMap.TryGetValue(key, out LinkedListNode<LRUCacheItem<TKey, TValue>> node))
            {
                if (DateTime.UtcNow > node.Value.ExpirationTime)
                {
                    Remove(key);
                    return false;
                }

                value = node.Value.Value;
                _lruList.Remove(node);
                _lruList.AddLast(node);
                return true;
            }
            return false;
        }

        private void RemoveFirst()
        {
            LinkedListNode<LRUCacheItem<TKey, TValue>> node = _lruList.First;
            _lruList.RemoveFirst();
            _cacheMap.Remove(node.Value.Key);
        }

        private void Remove(TKey key)
        {
            if (_cacheMap.TryGetValue(key, out LinkedListNode<LRUCacheItem<TKey, TValue>> node))
            {
                _lruList.Remove(node);
                _cacheMap.Remove(key);
            }
        }

        private void CleanupExpiredItems(object state)
        {
            var currentTime = DateTime.UtcNow;
            var nodesToRemove = new List<TKey>();

            foreach (var item in _lruList)
            {
                if (currentTime > item.ExpirationTime)
                {
                    nodesToRemove.Add(item.Key);
                }
                else
                {
                    // Items are ordered by access time, so we can stop once we find a non-expired item
                    break;
                }
            }

            foreach (var key in nodesToRemove)
            {
                Remove(key);
            }
        }
    }

    public class LRUCacheItem<TKey, TValue>
    {
        public TKey Key { get; set; }
        public TValue Value { get; set; }
        public DateTime ExpirationTime { get; set; }

        public LRUCacheItem(TKey key, TValue value, DateTime expirationTime)
        {
            Key = key;
            Value = value;
            ExpirationTime = expirationTime;
        }
    }
}
