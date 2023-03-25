using System.Collections.Concurrent;
using System.Threading.Channels;

namespace StoneAutomataConsole
{
    internal class ProducerArguments
    {
        public BlockingCollection<byte[,]> Collection { get; }
        public byte[,] InitialData { get; set; }
        public ProducerArguments(BlockingCollection<byte[,]> collection, byte[,] initialData)
        {
            Collection = collection;
            InitialData = initialData;
        }
    }
}
