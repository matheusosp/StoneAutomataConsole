using System.Collections.Concurrent;
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
        for (int i = 0; i < 1; i++)
        {
            Console.WriteLine(
                FindPath(Path.Combine(basePath, "input_l.txt"))
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

        ConcurrentDictionary<int, Context> toBeAdded = new ConcurrentDictionary<int, Context>(14, m.Length);

        byte[,] mr = new byte[rows, columns];
        int iter = 0;
        var final = new Context(' ', endingPoint.i, endingPoint.j, endingPoint.i * columns + endingPoint.j);
        while (true)
        {
            NextGen(m, mr, iUpperBound, jUpperBound);
            Parallel.ForEach(contexts, new ParallelOptions { MaxDegreeOfParallelism = 14 }, element => {
                var smr = MemoryMarshal.CreateSpan(ref mr[0, 0], mr.Length);
                // Current path is replaced by new paths
                AddPossiblePaths(element, smr, toBeAdded, iUpperBound, jUpperBound);
            });
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
    static void AddPossiblePaths(Context context, Span<byte> sm, ConcurrentDictionary<int, Context> destination, int iUpperBound, int jUpperBound)
    {
        int i = context.I;
        int j = context.J;

        int offset = context.Offset;
        int hashCode = offset - jUpperBound - 1;
        if (i > 0 && sm[hashCode] == 0 && !destination.ContainsKey(hashCode))
            destination.TryAdd(hashCode, new Context(context, 'U', i - 1, j, hashCode));

        hashCode = offset - 1;
        if (j > 0 && sm[hashCode] == 0 && !destination.ContainsKey(hashCode))
            destination.TryAdd(hashCode, new Context(context, 'L', i, j - 1, hashCode));

        hashCode = offset + jUpperBound + 1;
        if (i < iUpperBound && sm[hashCode] == 0 && !destination.ContainsKey(hashCode))
            destination.TryAdd(hashCode, new Context(context, 'D', i + 1, j, hashCode));

        hashCode = offset + 1;
        if (j < jUpperBound && sm[hashCode] == 0 && !destination.ContainsKey(hashCode))
            destination.TryAdd(hashCode, new Context(context, 'R', i, j + 1, hashCode));
    }

    static void Exchange(ref byte[,] m, ref byte[,] mr)
    {
        var tmp = m;
        m = mr;
        mr = tmp;
    }
    static void NextGen(byte[,] m, byte[,] mr, int iUpperBound, int jUpperBound)
    {
        NextGenCornersSlowPath(m, mr, iUpperBound, jUpperBound);
        
        int jLength = jUpperBound + 1;
        var sm = MemoryMarshal.CreateSpan(ref m[0, 0], m.Length);
        //Parallel.For(1, iUpperBound, new ParallelOptions { MaxDegreeOfParallelism = 14 }, i => 
        for (int i = 1; i < iUpperBound; i++)
        {
            int baseOffset = i * jLength;
            int j = 1;
            for (; j < jUpperBound - 1; j++)
            {
                int offset = baseOffset + j;
                mr[i, j] = GetGeneration(m[i, j],
                    CountNeighbours(ref m[i, j], jLength)
                );
            }
            // Slow path last item
            if (j < jUpperBound)
            {
                int offset = baseOffset + j;
                mr[i, j] = GetGeneration(m[i, j],
                    CountUpperLine(ref m[i, j], jLength)
                    + CountMiddleLine(ref m[i, j])
                    + m[i + 1, j - 1]
                    + m[i + 1, j]
                    + m[i + 1, j + 1]);
            }
        }
        //);
        mr[startingPoint.i, startingPoint.j] = 0;
        mr[endingPoint.i, endingPoint.j] = 0;
    }

    private static void NextGenCornersSlowPath(byte[,] m, byte[,] mr, int iUpperBound, int jUpperBound)
    {
        // Corner quadrants
        mr[0, 0] = GetGeneration(m[0, 0],
            m[0, 1]
            + CountTwo(ref m[1, 0]));
        mr[iUpperBound, 0] = GetGeneration(m[iUpperBound, 0],
            CountTwo(ref m[iUpperBound - 1, 0])
            + m[iUpperBound, 1]);
        mr[0, jUpperBound] = GetGeneration(m[0, jUpperBound],
            m[0, jUpperBound - 1]
            + CountTwo(ref m[1, jUpperBound - 1]));
        mr[iUpperBound, jUpperBound] = GetGeneration(m[iUpperBound, jUpperBound],
            CountTwo(ref m[iUpperBound - 1, jUpperBound - 1])
            + m[iUpperBound, jUpperBound - 1]);

        for (int i = 1; i < iUpperBound; i++)
        {
            mr[i, 0] = GetGeneration(m[i, 0],
                CountTwo(ref m[i - 1, 0])
                + m[i, 1]
                + CountTwo(ref m[i + 1, 0])
            );
            mr[i, jUpperBound] = GetGeneration(m[i, jUpperBound],
                CountTwo(ref m[i - 1, jUpperBound - 1])
                + m[i, jUpperBound - 1]
                + CountTwo(ref m[i + 1, jUpperBound - 1]));
        }
        int jLength = jUpperBound + 1;
        ref byte upper = ref m[iUpperBound, 0];
        for (int j = 1; j < jUpperBound; j++)
        {
            ref byte currentLower = ref m[0, j];
            mr[0, j] = GetGeneration(currentLower,
                CountLowerLine(ref currentLower, jLength)
                + CountMiddleLine(ref currentLower));

            ref byte currentUpper = ref Unsafe.Add(ref upper, j);
            mr[iUpperBound, j] = GetGeneration(currentUpper,
                 CountUpperLine(ref currentUpper, jLength)
                + CountMiddleLine(ref currentUpper));
        }
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int CountNeighbours(ref byte s, int jLength)
    {
        return CountUpperLine(ref s, jLength)
            + CountMiddleLine(ref s)
            + CountLowerLine(ref s, jLength);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]

    private static int CountMiddleLine(ref byte s)
    {
        return CountDonutLine(ref Unsafe.Add(ref s, -1));
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]

    private static int CountLowerLine(ref byte s, int jLength)
    {
        return CountCompleteLine(ref Unsafe.Add(ref s, -1 + jLength));
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]

    private static int CountUpperLine(ref byte s, int jLength)
    {
        return CountCompleteLine(ref Unsafe.Add(ref s, -1 - jLength));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CountCompleteLine(ref byte s)
    {
        const uint mask = 0x00_FF_FF_FF;
        ref uint r = ref Unsafe.As<byte, uint>(ref s);
        uint result = uint.PopCount(r & mask);
        return (int)result;
    }

    private static int CountTwo(ref byte s)
    {
        ref ushort r = ref Unsafe.As<byte, ushort>(ref s);
        uint result = ushort.PopCount(r);
        return (int)result;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CountDonutLine(ref byte s)
    {
        const uint mask = 0x00_FF_00_FF;
        uint result = uint.PopCount(Unsafe.As<byte, uint>(ref s) & mask);
        return (int)result;
    }

    static byte[,] ParseFile(string filePath)
    {
        var file = File.ReadAllLines(filePath);
        int rows = file.Length;
        int columns = file.First().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
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
