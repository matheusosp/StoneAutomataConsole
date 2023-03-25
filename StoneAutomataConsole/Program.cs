using StoneAutomataConsole;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal class Program
{
    private static (int i, int j) startingPoint;
    private static (int i, int j) endingPoint;
    private static volatile bool completed;
    private static void Main(string[] args)
    {
        string basePath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        for (int i = 0; i < 1; i++)
        {
            var sw = Stopwatch.StartNew();
            Console.WriteLine(
                FindPath(Path.Combine(basePath, "input_l.txt"))
            );
            sw.Stop();
            Console.WriteLine(sw.Elapsed);
        }
    }
    private static void ProduceGenerations(object? obj)
    {
        if (obj is ProducerArguments arguments)
        {
            var m = arguments.InitialData;

            int iUpperBound = m.GetUpperBound(0);
            int jUpperBound = m.GetUpperBound(1);

            int rows = iUpperBound + 1;
            int columns = jUpperBound + 1;
            while (!completed)
            {
                byte[,] mr = new byte[rows, columns];
                NextGen(m, mr, iUpperBound, jUpperBound);

                arguments.Collection.Add(mr);
                var sleep = (arguments.Collection.Count / 64) * 100;
                if (sleep > 0)
                    Thread.Sleep(sleep);
                Exchange(ref m, ref mr);
            }
            arguments.Collection.CompleteAdding();
        }
    }

    static string FindPath(string filePath)
    {
        byte[,] m = ParseFile(filePath);
        int iUpperBound = m.GetUpperBound(0);
        int jUpperBound = m.GetUpperBound(1);

        int rows = iUpperBound + 1;
        int columns = jUpperBound + 1;

        m[startingPoint.i, startingPoint.j] = 0;
        m[endingPoint.i, endingPoint.j] = 0;

        var collection = new BlockingCollection<byte[,]>(1024);
        var producer = new Thread(ProduceGenerations);
        producer.Start(new ProducerArguments(collection, m));

        Context? found = null;
        HashSet<Context> contexts = new HashSet<Context>(m.Length) {
            new Context(' ', startingPoint.i, startingPoint.j, startingPoint.i * columns + startingPoint.j)
        };

        List<Context> toBeRemoved = new List<Context>(m.Length);
        Dictionary<int, Context> toBeAdded = new Dictionary<int, Context>(m.Length);

        var final = new Context(' ', endingPoint.i, endingPoint.j, endingPoint.i * columns + endingPoint.j);
        //Console.WriteLine("Original");
        //Write(m);
        for (int i = 0; i < int.MaxValue; i++)
        {
            var mr = collection.Take();
            var smr = MemoryMarshal.CreateSpan(ref mr[0, 0], mr.Length);
            foreach (var element in contexts)
            {
                toBeRemoved.Add(element);
                AddPossiblePaths(element, smr, toBeAdded, iUpperBound, jUpperBound);
            }
            foreach (var element in toBeRemoved)
                contexts.Remove(element);
            toBeRemoved.Clear();
            foreach (var element in toBeAdded)
                contexts.Add(element.Value);
            toBeAdded.Clear();
            if (contexts.TryGetValue(final, out found))
            {
                Console.WriteLine("Found solution");
                completed = true;

                return WriteContext(found);
            }
            if (contexts.Count == 0)
            {
                Console.WriteLine("Sem caminho possível");
                completed = true;

                return "";
            }
            //Console.WriteLine($"Iteration {i + 1}");
            //(int left, int top) = Console.GetCursorPosition();
            //int line = 0;
            //foreach (var item in contexts)
            //{
            //    Console.SetCursorPosition(left + 50, top + line++);
            //    Console.WriteLine(WriteContext(item));
            //}
            //Console.SetCursorPosition(left, top);
            //Write(mr);
        }
        Console.WriteLine("Caminho longe demais");
        completed = true;
        producer.Join();
        return "";
    }

    static string WriteContext(Context? found)
    {
        Stack<Context> reversed = new Stack<Context>();
        while(found != null)
        {
            reversed.Push(found);
            found = found.Parent;
        }
        return string.Join(' ', reversed.Skip(1).Select(c => c.Direction).ToArray());
    }

    static void AddPossiblePaths(Context context, Span<byte> sm, Dictionary<int, Context> destination, int iUpperBound, int jUpperBound)
    {
        int i = context.I;
        int j = context.J;

        int offset = context.Offset;
        int hashCode = offset - jUpperBound - 1;
        if (i > 0 && sm[hashCode] == 0 && !destination.ContainsKey(hashCode))
            destination.Add(hashCode, new Context(context, 'U', i - 1, j, hashCode));

        hashCode = offset - 1;
        if (j > 0 && sm[hashCode] == 0 && !destination.ContainsKey(hashCode))
            destination.Add(hashCode, new Context(context, 'L', i, j - 1, hashCode));

        hashCode = offset + jUpperBound + 1;
        if (i < iUpperBound && sm[hashCode] == 0 && !destination.ContainsKey(hashCode))
            destination.Add(hashCode, new Context(context, 'D', i + 1, j, hashCode));

        hashCode = offset + 1;
        if (j < jUpperBound && sm[hashCode] == 0 && !destination.ContainsKey(hashCode))
            destination.Add(hashCode, new Context(context, 'R', i, j + 1, hashCode));
    }

    static void Exchange(ref byte[,] m, ref byte[,] mr)
    {
        var tmp = m;
        m = mr;
        mr = tmp;
    }
    static void NextGen(byte[,] m, byte[,] mr, int iUpperBound, int jUpperBound)
    {
        for (int i = 0; i <= iUpperBound; i++)
        {
            mr[i, 0] = GetGeneration(m[i, 0],
                SlowPathScore(m, i, 0, iUpperBound, jUpperBound));
            mr[i, jUpperBound] = GetGeneration(m[i, jUpperBound],
                SlowPathScore(m, i, jUpperBound, iUpperBound, jUpperBound));
        }
        for (int j = 0; j <= jUpperBound; j++)
        {
            mr[0, j] = GetGeneration(m[0, j],
                SlowPathScore(m, 0, j, iUpperBound, jUpperBound));
            mr[iUpperBound, j] = GetGeneration(m[iUpperBound, j],
                SlowPathScore(m, iUpperBound, j, iUpperBound, jUpperBound));
        }

        int jLength = jUpperBound + 1;
        var sm = MemoryMarshal.CreateSpan(ref m[0, 0], m.Length);
        for (int i = 1; i < iUpperBound; i++)
        //Parallel.For(1, iUpperBound, i =>
        {
            int baseOffset = i * jLength;
            int j = 1;
            for (; j < jUpperBound - 1; j++)
            {
                int offset = baseOffset + j;
                mr[i, j] = GetGeneration(sm[offset],
                    CountNeighbours(ref sm[offset], jLength)
                );
            }
            if (j < jUpperBound)
                mr[i, jUpperBound - 1] = GetGeneration(m[i, j],
                    m[i - 1, j - 1]
                    + m[i - 1, j]
                    + m[i - 1, j + 1]
                    + m[i, j - 1]
                    + m[i, j + 1]
                    + m[i + 1, j - 1]
                    + m[i + 1, j]
                    + m[i + 1, j + 1]);
        }
        //);
        mr[startingPoint.i, startingPoint.j] = 0;
        mr[endingPoint.i, endingPoint.j] = 0;
    }
    
    static ReadOnlySpan<byte> map => new byte[] { 
        0, 0, 1, 1, 1, 0, 0, 0, 0,
        0, 0, 0, 0, 1, 1, 0, 0, 0
    };
    static byte GetGeneration(byte currentValue, int score)
    {
        return map[currentValue * 9 + score];
    }

    static int CountNeighbours(ref byte s, int jLength)
    {
        uint mask = uint.MaxValue >> 8;
        uint maskMiddle = mask ^ (255 << 8);

        return (int)(uint.PopCount(Unsafe.As<byte, uint>(ref Unsafe.Add(ref s, - 1 - jLength)) & mask)
            + uint.PopCount(Unsafe.As<byte, uint>(ref Unsafe.Add(ref s, - 1)) & maskMiddle)
            + uint.PopCount(Unsafe.As<byte, uint>(ref Unsafe.Add(ref s, - 1 + jLength)) & mask));
    }

    private static int SlowPathScore(byte[,] m, int i, int j, int iUpperBound, int jUpperBound)
    {
        int score = 0;
        if (i > 0 && j > 0)
            score += m[i - 1, j - 1];
        if (i > 0)
            score += m[i - 1, j];
        if (j < jUpperBound && i > 0)
            score += m[i - 1, j + 1];

        if (j > 0)
            score += m[i, j - 1];
        if (j < jUpperBound)
            score += m[i, j + 1];

        if (j > 0 && i < iUpperBound)
            score += m[i + 1, j - 1];
        if (i < iUpperBound)
            score += m[i + 1, j];
        if (i < iUpperBound && j < jUpperBound)
            score += m[i + 1, j + 1];
        return score;
    }

    static byte[,] ParseFile(string filePath)
    {
        var file = File.ReadAllLines(filePath);
        int rows = file.Length;
        int columns = file.First().Split(' ').Length;
        byte[,] m = new byte[rows, columns];
        for (int i = 0; i < rows; i++)
        {
            string[] data = file[i].Split(' ');
            for (int j = 0; j < columns; j++)
            {
                switch (m[i, j] = byte.Parse(data[j]))
                {
                    case 3:
                        startingPoint = (i, j);
                        break;
                    case 4:
                        endingPoint = (i, j);
                        break;
                }
            }
        }
        return m;
    }
}
