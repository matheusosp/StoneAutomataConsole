using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

internal class Program
{
    private static (int i, int j) startingPoint;
    private static (int i, int j) endingPoint;
    private static void Main(string[] args)
    {
        string basePath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        for (int i = 0; i < 1; i++)
        {
            var sw = Stopwatch.StartNew();
            Console.WriteLine(
                FindPath(Path.Combine(basePath, "input.txt"))
            );
            sw.Stop();
            Console.WriteLine(sw.Elapsed);
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

        Context? found = null;
        HashSet<Context> contexts = new HashSet<Context>(m.Length) {
            new Context(' ', startingPoint.i, startingPoint.j, startingPoint.i * columns + startingPoint.j)
        };

        List<Context> toBeRemoved = new List<Context>(m.Length);
        Dictionary<int, Context> toBeAdded = new Dictionary<int, Context>(m.Length);

        byte[,] mr = new byte[rows, columns];

        var final = new Context(' ', endingPoint.i, endingPoint.j, endingPoint.i * columns + endingPoint.j);
        while (true)
        {
            NextGen(m, mr, iUpperBound, jUpperBound);
            var smr = MemoryMarshal.CreateSpan(ref mr[0, 0], mr.Length);
            foreach (var element in contexts)
            {
                // Current path is replaced by new paths
                toBeRemoved.Add(element);
                AddPossiblePaths(element, smr, toBeAdded, iUpperBound, jUpperBound);
            }
            // Remove deadends and replaced paths
            foreach (var element in toBeRemoved)
                contexts.Remove(element);
            toBeRemoved.Clear();

            // Add new paths
            foreach (var element in toBeAdded)
                contexts.Add(element.Value);
            toBeAdded.Clear();

            // if touched final destination, solution is found
            if (contexts.TryGetValue(final, out found))
            {
                Console.WriteLine("Found solution");
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
        NextGenCornersSlowPath(m, mr, iUpperBound, jUpperBound);
        
        int jLength = jUpperBound + 1;
        var sm = MemoryMarshal.CreateSpan(ref m[0, 0], m.Length);
        for (int i = 1; i < iUpperBound; i++)
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
            // Slow path last item
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
        mr[startingPoint.i, startingPoint.j] = 0;
        mr[endingPoint.i, endingPoint.j] = 0;
    }

    private static void NextGenCornersSlowPath(byte[,] m, byte[,] mr, int iUpperBound, int jUpperBound)
    {
        // Corner quadrants
        mr[0, 0] = GetGeneration(m[0, 0],
            m[0, 1]
            + m[1, 0]
            + m[1, 1]);
        mr[iUpperBound, 0] = GetGeneration(m[iUpperBound, 0],
            m[iUpperBound - 1, 0]
            + m[iUpperBound - 1, 1]
            + m[iUpperBound, 1]);
        mr[0, jUpperBound] = GetGeneration(m[0, jUpperBound],
            m[0, jUpperBound - 1]
            + m[1, jUpperBound - 1]
            + m[1, jUpperBound]);
        mr[iUpperBound, jUpperBound] = GetGeneration(m[iUpperBound, jUpperBound],
            m[iUpperBound - 1, jUpperBound] 
            + m[iUpperBound, jUpperBound - 1] 
            + m[iUpperBound - 1, jUpperBound - 1]);

        for (int i = 1; i < iUpperBound; i++)
        {
            mr[i, 0] = GetGeneration(m[i, 0],
                  m[i - 1, 1]
                + m[i, 1]
                + m[i + 1, 1]
                + m[i - 1, 0]
                + m[i + 1, 0]);
            mr[i, jUpperBound] = GetGeneration(m[i, jUpperBound],
                m[i - 1, jUpperBound - 1]
                + m[i - 1, jUpperBound]
                + m[i, jUpperBound - 1]
                + m[i + 1, jUpperBound - 1]
                + m[i + 1, jUpperBound]);
        }
        for (int j = 1; j < jUpperBound; j++)
        {
            mr[0, j] = GetGeneration(m[0, j],
                m[0, j - 1]
                + m[0, j + 1]
                + m[1, j - 1]
                + m[1, j]
                + m[1, j + 1]);
            mr[iUpperBound, j] = GetGeneration(m[iUpperBound, j],
                m[iUpperBound - 1, j - 1]
                + m[iUpperBound - 1, j]
                + m[iUpperBound - 1, j + 1]
                + m[iUpperBound, j - 1]
                + m[iUpperBound, j + 1]);
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
    static int CountNeighbours(ref byte s, int jLength)
    {
        return (int)(CountCompleteLine(ref Unsafe.Add(ref s, -1 - jLength))
            + CountDonutLine(ref Unsafe.Add(ref s, -1))
            + CountCompleteLine(ref Unsafe.Add(ref s, -1 + jLength)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint CountCompleteLine(ref byte s)
    {
        const uint mask = 0x00_FF_FF_FF;
        ref uint r = ref Unsafe.As<byte, uint>(ref s);
        uint result = uint.PopCount(r & mask);
        return result;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint CountDonutLine(ref byte s)
    {
        const uint mask = 0x00_FF_00_FF;
        uint result = uint.PopCount(Unsafe.As<byte, uint>(ref s) & mask);
        return result;
    }

    /// <summary>
    /// Sums up the slow way, validating all corners
    /// </summary>
    /// <param name="m"></param>
    /// <param name="i"></param>
    /// <param name="j"></param>
    /// <param name="iUpperBound"></param>
    /// <param name="jUpperBound"></param>
    /// <returns></returns>
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
