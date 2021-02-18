using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Socks5Server
{
    public static class QueueManager
    {
        public static ConcurrentDictionary<string, ConcurrentQueue<Socks5State>> Queue { get; set; } = new ConcurrentDictionary<string, ConcurrentQueue<Socks5State>>();

        public static void EnqueueElement(string guid, Socks5State state)
        {
            KeyValuePair<string, ConcurrentQueue<Socks5State>> item;

            item = Queue.FirstOrDefault(_ => _.Key == guid);

            if (item.Key != null)
            {
                item.Value.Enqueue(state);
            }
            else
            {
                var concurrentQueue = new ConcurrentQueue<Socks5State>();
                concurrentQueue.Enqueue(state);
                Queue.TryAdd(guid, concurrentQueue);
            }
        }

        public static Socks5State DequeueElement(string guid)
        {
            KeyValuePair<string, ConcurrentQueue<Socks5State>> item;

            item = Queue.FirstOrDefault(_ => _.Key == guid);

            Socks5State result = null;
            if (item.Key != null)
            {
                item.Value.TryDequeue(out result);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }
    }
}
