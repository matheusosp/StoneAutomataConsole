using System.Diagnostics;
using System.Net.Mime;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal class Program
{
    private static (int i, int j) startingPoint;
    private static (int i, int j) endingPoint;
    private static void Main(string[] args)
    {

        string basePath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string filePath = Path.Combine(basePath, "input_l.txt");
        string result = null;
        Stopwatch sw = new Stopwatch();
        int loop = 100;
        for (int i = 0; i < loop; i++)
        {
            byte[,] m = ParseFile(filePath);
            sw.Start();
            result = FindPath(m);
            sw.Stop();
        }
        Console.WriteLine(sw.ElapsedMilliseconds / loop);
        Console.WriteLine(result);
    }

    static string FindPath(byte[,] m)
    {
        int iUpperBound = m.GetUpperBound(0);
        int jUpperBound = m.GetUpperBound(1);

        int rows = iUpperBound + 1;
        int columns = jUpperBound + 1;

        m[startingPoint.i, startingPoint.j] = 0;
        m[endingPoint.i, endingPoint.j] = 0;

        List<Context> contexts = new List<Context>(m.Length / 4)
        { 
            new Context(' ', startingPoint.i, startingPoint.j, startingPoint.i * columns + startingPoint.j)
        };

        //ConcurrentDictionary<int, Context> toBeAdded = new ConcurrentDictionary<int, Context>(3, m.Length);
        Dictionary<int, Context> toBeAdded = new Dictionary<int, Context>(m.Length / 2);

        byte[,] mr = new byte[rows, columns];
        var final = new Context(' ', endingPoint.i, endingPoint.j, endingPoint.i * columns + endingPoint.j);

        while (true)
        {
            NextGen(m, mr, iUpperBound, jUpperBound);
            //Parallel.ForEach(contexts, new ParallelOptions { MaxDegreeOfParallelism = 4 }, element =>
            var smr = MemoryMarshal.CreateSpan(ref mr[0, 0], mr.Length);

            var s = CollectionsMarshal.AsSpan(contexts);
            for (int i = 0; i < s.Length; i++)
            {
                var element = s[i];
                // Current path is replaced by new paths
                AddPossiblePaths(element, smr, toBeAdded, iUpperBound, jUpperBound);
            }
            //);
            // if touched final destination, solution is found
            if (toBeAdded.TryGetValue(final.Offset, out Context? found))
                return ExtractSteps(found);

            // Remove deadends and replaced paths
            contexts.Clear();

            // Add new paths
            foreach (var element in toBeAdded)
                contexts.Add(element.Value);
            toBeAdded.Clear();
            
            // No more paths to take
            if (contexts.Count == 0)
            {
                Console.WriteLine("Impossible maze");
                return "";
            }
            Swap(ref m, ref mr);
        }
    }

    static string ExtractSteps(Context? found)
    {
        Stack<Context> reversed = new Stack<Context>(512);
        while(found != null)
        {
            reversed.Push(found);
            found = found.Parent;
        }
        var result = reversed.Skip(1).Select(c => c.Direction).ToArray();
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
        if (i < iUpperBound - 1 && sm[hashCode] == 0 && !destination.ContainsKey(hashCode))
            destination.Add(hashCode, new Context(context, 'D', i + 1, j, hashCode));

        hashCode = offset + 1;
        if (j < jUpperBound - 1 && sm[hashCode] == 0 && !destination.ContainsKey(hashCode))
            destination.Add(hashCode, new Context(context, 'R', i, j + 1, hashCode));
    }

    static void Swap(ref byte[,] m, ref byte[,] mr)
    {
        var tmp = m;
        m = mr;
        mr = tmp;
    }
    static ReadOnlySpan<byte> map => new byte[] {
        0, 0, 1, 1, 1, 0, 0, 0, 0,
        0, 0, 0, 0, 1, 1, 0, 0, 0
    };

    static void NextGen(byte[,] m, byte[,] mr, int iUpperBound, int jUpperBound)
    {
        int jLength = jUpperBound + 1;
        //Parallel.For(1, iUpperBound, new ParallelOptions { MaxDegreeOfParallelism = 10 }, i => 
        for (int i = 1; i < iUpperBound; i++)
        {
            ref byte currentResultRow = ref mr[i, 0];
            ref byte currentRow = ref m[i, 0];
            for (int j = 1; j < jUpperBound; j++)
            {
                ref byte current = ref Unsafe.Add(ref currentRow, j);
                Unsafe.Add(ref currentResultRow, j) = map[current * 9 + 
                    CountNeighbours(ref current, jLength)
                ];
            }
        }
        //);
        mr[startingPoint.i, startingPoint.j] = 0;
        mr[endingPoint.i, endingPoint.j] = 0;
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
        return (int)uint.PopCount(Unsafe.As<byte, uint>(ref Unsafe.Add(ref s, -1 - jLength)) & 0x00_FF_FF_FF)
            + (int)uint.PopCount(Unsafe.As<byte, uint>(ref Unsafe.Add(ref s, -1)) & 0x00_FF_00_FF)
            + (int)uint.PopCount(Unsafe.As<byte, uint>(ref Unsafe.Add(ref s, -1 + jLength)) & 0x00_FF_FF_FF);
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
