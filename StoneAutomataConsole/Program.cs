using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal class Program
{
    private static (int i, int j) startingPoint;
    private static (int i, int j) endingPoint;
    private static void Main(string[] args)
    {

        string basePath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        for (int i = 0; i < 10; i++)
        {
            Console.WriteLine(
                FindPath(Path.Combine(basePath, "input.txt"))
            );
        }
    }

    static string FindPath(string filePath)
    {
        byte[,] m = ParseFile(filePath);
        var sw = Stopwatch.StartNew();

        int iUpperBound = m.GetUpperBound(0);
        int jUpperBound = m.GetUpperBound(1);

        int rows = iUpperBound + 1;
        int columns = jUpperBound + 1;

        m[startingPoint.i, startingPoint.j] = 0;
        m[endingPoint.i, endingPoint.j] = 0;

        Context? found = null;
        HashSet<Context> contexts = new HashSet<Context>(m.Length) {
            new Context(' ', startingPoint.i, startingPoint.j, startingPoint.i * columns + startingPoint.j)
        };

        //ConcurrentDictionary<int, Context> toBeAdded = new ConcurrentDictionary<int, Context>(3, m.Length);
        Dictionary<int, Context> toBeAdded = new Dictionary<int, Context>(m.Length);

        byte[,] mr = new byte[rows, columns];
        int iter = 0;
        var final = new Context(' ', endingPoint.i, endingPoint.j, endingPoint.i * columns + endingPoint.j);
        while (true)
        {
            //var swStep = Stopwatch.StartNew();
            NextGen(m, mr, iUpperBound, jUpperBound);
            //swStep.Stop();
            //Console.WriteLine($"NextGen elapsed {swStep.Elapsed}");
            
            //swStep = Stopwatch.StartNew();
            //Parallel.ForEach(contexts, new ParallelOptions { MaxDegreeOfParallelism = 4 }, element =>
            var smr = MemoryMarshal.CreateSpan(ref mr[0, 0], mr.Length);
            foreach (var element in contexts)
            {
                // Current path is replaced by new paths
                AddPossiblePaths(element, smr, toBeAdded, iUpperBound, jUpperBound);
            }
            //);
            //swStep.Stop();
            //Console.WriteLine($"AddPossiblePaths elapsed {swStep.Elapsed}");

            // Remove deadends and replaced paths
            contexts.Clear();

            // Add new paths
            foreach (var element in toBeAdded)
                contexts.Add(element.Value);
            toBeAdded.Clear();
            if (++iter % 1000 == 0)
            {
                Console.WriteLine($"Contexts: {contexts.Count}");
            }
            // if touched final destination, solution is found
            if (contexts.TryGetValue(final, out found))
            {
                Console.WriteLine("Found solution");
                sw.Stop();
                Console.WriteLine(sw.Elapsed);
                return ExtractSteps(found);
            }
            // No more paths to take
            if (contexts.Count == 0)
            {
                Console.WriteLine("Impossible maze");
                return "";
            }
            Exchange(ref m, ref mr);
        }
    }

    static string ExtractSteps(Context? found)
    {
        Stack<Context> reversed = new Stack<Context>();
        while(found != null)
        {
            reversed.Push(found);
            found = found.Parent;
        }
        var result = reversed.Skip(1).Select(c => c.Direction).ToArray();
        Console.WriteLine(result.Length);
        return string.Join(' ', result);
    }

    /// <summary>
    /// Add possible paths if is white path, not cornered and not redundant
    /// Other path is already in this same coordinate
    /// </summary>
    /// <param name="context"></param>
    /// <param name="sm"></param>
    /// <param name="destination"></param>
    /// <param name="iUpperBound"></param>
    /// <param name="jUpperBound"></param>
    static void AddPossiblePaths(Context context, Span<byte> sm,
        //ConcurrentDictionary<int, Context> destination, 
        Dictionary<int, Context> destination,
        int iUpperBound, int jUpperBound)
    {
        int i = context.I;
        int j = context.J;

        int offset = context.Offset;
        int hashCode = offset - jUpperBound - 1;
        if (i > 1 && sm[hashCode] == 0 && !destination.ContainsKey(hashCode))
            destination.Add(hashCode, new Context(context, 'U', i - 1, j, hashCode));

        hashCode = offset - 1;
        if (j > 1 && sm[hashCode] == 0 && !destination.ContainsKey(hashCode))
            destination.Add(hashCode, new Context(context, 'L', i, j - 1, hashCode));

        hashCode = offset + jUpperBound + 1;
        if (i < iUpperBound - 1&& sm[hashCode] == 0 && !destination.ContainsKey(hashCode))
            destination.Add(hashCode, new Context(context, 'D', i + 1, j, hashCode));

        hashCode = offset + 1;
        if (j < jUpperBound - 1 && sm[hashCode] == 0 && !destination.ContainsKey(hashCode))
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
        int jLength = jUpperBound + 1;
        Parallel.For(1, iUpperBound, new ParallelOptions { MaxDegreeOfParallelism = 10 }, i => 
        //for (int i = 1; i < iUpperBound; i++)
        {
            ref byte currentResultRow = ref mr[i, 0];
            ref byte currentRow = ref m[i, 0];
            for (int j = 1; j < jUpperBound; j++)
            {
                ref byte current = ref Unsafe.Add(ref currentRow, j);
                Unsafe.Add(ref currentResultRow, j) = GetGeneration(current,
                    CountNeighbours(ref current, jLength)
                );
            }
        }
        );
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

    /// <summary>
    /// Count neighbours with integer PopCount and masks
    /// Matrix with the values:
    ///     1, 0, 1, 1
    /// Became Int32 with the following (hex):
    ///     01 00 01 01
    /// Mask (corners) applied with bitwise AND
    ///     00 FF FF FF
    /// Results in:
    ///     00 00 01 01
    /// Population count gives 2
    /// </summary>
    /// <param name="s"></param>
    /// <param name="jLength"></param>
    /// <returns></returns>
    static int CountNeighbours(ref byte s, int jLength)
    {
        return CountUpperLine(ref s, jLength)
            + CountMiddleLine(ref s)
            + CountLowerLine(ref s, jLength);
    }

    private static int CountMiddleLine(ref byte s)
    {
        return CountDonutLine(ref Unsafe.Add(ref s, -1));
    }

    private static int CountLowerLine(ref byte s, int jLength)
    {
        return CountCompleteLine(ref Unsafe.Add(ref s, -1 + jLength));
    }

    private static int CountUpperLine(ref byte s, int jLength)
    {
        return CountCompleteLine(ref Unsafe.Add(ref s, -1 - jLength));
    }

    private static int CountCompleteLine(ref byte s)
    {
        const uint mask = 0x00_FF_FF_FF;
        return (int)uint.PopCount(
            Unsafe.As<byte, uint>(ref s) & mask);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CountDonutLine(ref byte s)
    {
        const uint mask = 0x00_FF_00_FF;
        return (int)uint.PopCount(Unsafe.As<byte, uint>(ref s) & mask);
    }

    static byte[,] ParseFile(string filePath)
    {
        var file = File.ReadAllLines(filePath);
        int rows = file.Length;
        int columns = file.First().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        byte[,] m = new byte[rows + 2, columns + 2];
        for (int i = 0; i < rows; i++)
        {
            string[] data = file[i].Split(' ');
            for (int j = 0; j < columns; j++)
            {
                switch (m[i + 1, j + 1] = byte.Parse(data[j]))
                {
                    case 3:
                        startingPoint = (i + 1, j + 1);
                        break;
                    case 4:
                        endingPoint = (i + 1, j + 1);
                        break;
                }
            }
        }
        return m;
    }
}
