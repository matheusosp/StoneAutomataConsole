using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal class Program
{
    private static (int i, int j) startingPoint;
    private static (int i, int j) endingPoint;

    private static (int i, int j) startingBound;
    private static (int i, int j) endingBound;
    
    private static byte[] mask;

    private static void Main(string[] args) 
    {
        string basePath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string filePath = Path.Combine(basePath, "input5.txt");
        string result = null;
        Stopwatch sw = new Stopwatch();
        long sum = 0;
        long min = long.MaxValue;
        long max = long.MinValue;
        int loop = 1;
        byte[,] m = null;
        int steps = 49991;
        for (int i = 0; i < loop; i++)
        {
            m = ParseFile(filePath);
            GC.Collect();
            sw.Restart();
            var context = FindContext(m, steps);
            m = ParseFile(filePath);
            var stat = ExtractStatistics(context, m);
            for (int generation = 2; generation < stat.Boards.Count; generation++)
            {
                TryAppendIncrementalContext(generation, stat);
            }
            foreach (var item in stat.Paths)
            {
                Console.WriteLine(item);
            }
            sw.Stop();
            sum += sw.ElapsedTicks;
            min = long.Min(min, sw.ElapsedTicks);
            max = long.Max(max, sw.ElapsedTicks);
        }
        Console.WriteLine($"Size: {endingBound.i + 1}x{endingBound.j + 1}");
        Console.WriteLine($"Avg: {TimeSpan.FromTicks(sum / loop)}, Min: {TimeSpan.FromTicks(min)}, Max: {TimeSpan.FromTicks(max)}");
    }

    static Context FindContext(byte[,] m, int steps)
    {
        int iLength = m.GetUpperBound(0) + 1;
        int jLength = m.GetUpperBound(1) + 1;

        m[startingPoint.i, startingPoint.j] = 0;
        m[endingPoint.i, endingPoint.j] = 0;

        List<Context> contexts = new List<Context>(m.Length / 4)
        { 
            new Context(0, ' ', startingPoint.i, startingPoint.j, startingPoint.i * jLength + startingPoint.j)
        };

        Context?[] toBeAdded = new Context?[m.Length];
        List<int> toBeAddedIndex = new List<int>(m.Length);

        byte[,] mr = new byte[iLength, jLength];
        var final = new Context(0, ' ', endingPoint.i, endingPoint.j, endingPoint.i * jLength + endingPoint.j);
        int diagonal = (int)(Math.Sqrt(Math.Pow(endingPoint.i + 1, 2) + Math.Pow(endingPoint.j + 1, 2)) * 1.5);
        int step = 0;
        while (true)
        {
            NextGen(m, mr, jLength);
            var smr = MemoryMarshal.CreateSpan(ref mr[0, 0], mr.Length);
            var s = CollectionsMarshal.AsSpan(contexts);
            for (int i = 0; i < s.Length; i++)
            {
                var element = s[i];
                // Current path is replaced by new paths
                AddPossiblePaths(element, smr, toBeAdded, toBeAddedIndex, jLength);
            }
            // if touched final destination, solution is found
            if (toBeAdded[final.Offset] is Context found)
            {
                if (Count(found) >= steps)
                    return found;
            }
            // Remove deadends and replaced paths
            contexts.Clear();

            // Add new paths
            foreach (var index in toBeAddedIndex)
            {
                //Console.WriteLine($"Step: {step} - {ExtractSteps(element.Value)}");
                var element = toBeAdded[index];
                contexts.Add(element);
                toBeAdded[index] = null;
            }
            toBeAddedIndex.Clear();

            step++;
            if (contexts.Count > diagonal)
            {
                var itens = contexts.OrderByDescending(c => c.J + c.I).Skip(diagonal).ToArray();
                foreach (var item in itens)
                    contexts.Remove(item);
            }

            // No more paths to take
            if (contexts.Count == 0)
            {
                Console.WriteLine("Impossible maze");
                return null;
            }
            Swap(ref m, ref mr);
        }
    }

    static bool TryAppendIncrementalContext(int initial, ContextStatistics statistics)
    {
        var m = statistics.Boards[initial];
        if (m[0, 0] == 1)
            return false;
        int jLength = m.GetUpperBound(1) + 1;
        List<Context> contexts = new List<Context>(statistics.Boards.Count)
        {
            new Context(initial, ' ', startingPoint.i, startingPoint.j, startingPoint.i * jLength + startingPoint.j)
        };
        var final = new Context(0, ' ', endingPoint.i, endingPoint.j, endingPoint.i * jLength + endingPoint.j);

        Context?[] toBeAdded = new Context?[m.Length];
        List<int> toBeAddedIndex = new List<int>(m.Length);
        Context? last = null;
        int diagonal = (int)(Math.Sqrt(Math.Pow(endingPoint.i + 1, 2) + Math.Pow(endingPoint.j + 1, 2)) * 1.5);
        int step = 0;
        for (int boardIndex = initial; boardIndex < statistics.Boards.Count; boardIndex++)
        {
            var mr = statistics.Boards[boardIndex];
            var smr = MemoryMarshal.CreateSpan(ref mr[0, 0], mr.Length);
            var s = CollectionsMarshal.AsSpan(contexts);
            for (int i = 0; i < s.Length; i++)
            {
                var element = s[i];
                // Current path is replaced by new paths
                AddPossiblePaths(element, smr, toBeAdded, toBeAddedIndex, jLength);
            }
            if (toBeAdded[final.Offset] != null)
            {
                last = toBeAdded[final.Offset];
                break;
            }
            if (toBeAddedIndex.Count == 0 || boardIndex == statistics.Boards.Count - 1)
            {
                last = contexts.First();
                break;
            }
            contexts.Clear();
            foreach (var index in toBeAddedIndex)
            {
                var element = toBeAdded[index];
                contexts.Add(element);
                toBeAdded[index] = null;
            }
            toBeAddedIndex.Clear();
            step++;
            if (contexts.Count > diagonal)
            {
                var itens = contexts.OrderByDescending(c => c.J + c.I).Skip(diagonal).ToArray();
                foreach (var item in itens)
                    contexts.Remove(item);
            }
        }
        if (last != null)
        {
            FillStatistics(statistics, last);
            return true;
        }
        return false;
    }
    static int Count(Context? found)
    {
        int count = 0;
        while (found != null)
        {
            count++;
            found = found.Parent;
        }
        return count;
    }
    static void FillStatistics(ContextStatistics statistics, Context? found)
    {
        Stack<Context> reversed = new Stack<Context>(512);
        var initial = found;
        while (found != null)
        {
            reversed.Push(found);
            found = found.Parent;
        }
        if (reversed.Count > 1)
        {
            string path = initial.Generation.ToString()
                + " "
                + string.Join(' ', reversed.Skip(1).Select(c => c.Direction).ToArray());
            statistics.Paths.Add(path);
            var initialGeneration = initial.Generation;
            while (reversed.TryPop(out Context popped))
            {
                statistics.Boards[initialGeneration++][popped.I, popped.J] = 1;
            }
        }
    }
    static ContextStatistics ExtractStatistics(Context? found, byte[,] initialBoard)
    {
        Stack<Context> reversed = new Stack<Context>(512);
        var initial = found;
        while (found != null)
        {
            reversed.Push(found);
            found = found.Parent;
        }
        ContextStatistics statistics = new ContextStatistics();
        statistics.Paths.Add(initial.Generation.ToString() 
            + " " 
            + string.Join(' ', reversed.Skip(1).Select(c => c.Direction).ToArray()));

        byte[,] board = initialBoard;
        int jLength = board.GetUpperBound(1) + 1;
        while (reversed.TryPop(out Context popped))
        {
            statistics.Boards.Add(board);
            var next = new byte[board.GetUpperBound(0) + 1, board.GetUpperBound(1) + 1];
            NextGen(board, next, jLength);
            board[popped.I, popped.J] = 1;
            board = next;
        }
        return statistics;
    }
    //static string ExtractSteps(Context? found)
    //{
    //    Context? initial = found;
    //    int length = 0;
    //    while(found != null)
    //    {
    //        length++;
    //        found = found.Parent;
    //    }
    //    return string.Create(length - 1, initial, 
    //        static (span, state) => {
    //            int length = span.Length;
    //            for (int i = span.Length - 1; i >= 0; i--)
    //            {
    //                span[i] = state.Direction;
    //                state = state.Parent;
    //            }
    //        });
    //}

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
        Context?[] destination,
        List<int> destinationIndex,
        int jLength)
    {
        {
            int hashCode = context.Offset - jLength;
            if (sm[hashCode] == 0 && context.I > startingBound.i)
            {
                ref Context? item = ref destination[hashCode];
                if (item == null)
                {
                    destinationIndex.Add(hashCode);
                    item = new Context(context, 'U', context.I - 1, context.J, hashCode);
                }
            }
        }
        {
            int hashCode = context.Offset - 1;
            if (sm[hashCode] == 0 && context.J > startingBound.j)
            {
                ref Context? item = ref destination[hashCode];
                if (item == null)
                {
                    destinationIndex.Add(hashCode);
                    item = new Context(context, 'L', context.I, context.J - 1, hashCode);
                }
            }
        }
        {
            int hashCode = context.Offset + jLength;
            if (sm[hashCode] == 0 && context.I < endingBound.i)
            {
                ref Context? item = ref destination[hashCode];
                if (item == null)
                {
                    destinationIndex.Add(hashCode);
                    item = new Context(context, 'D', context.I + 1, context.J, hashCode);
                }
            }
        }
        {
            int hashCode = context.Offset + 1;
            if (sm[hashCode] == 0 && context.J < endingBound.j)
            {
                ref Context? item = ref destination[hashCode];
                if (item == null)
                {
                    destinationIndex.Add(hashCode);
                    item = new Context(context, 'R', context.I, context.J + 1, hashCode);
                }
            }
        }
    }
    static void Swap(ref byte[,] m, ref byte[,] mr)
    {
        var tmp = m;
        m = mr;
        mr = tmp;
    }
    static void NextGen(byte[,] m, byte[,] mr, int jLength)
    {
        for (int i = startingBound.i; i <= endingBound.i; i++)
        {
            ref byte currentResultRow = ref mr[i, 0];
            ref byte currentRow = ref m[i, 0];
            int j = startingBound.j;
            for (; j < endingBound.j - Vector<byte>.Count; j += Vector<byte>.Count)
            {
                Vector<byte> neighbours 
                    = Unsafe.As<byte, Vector<byte>>(ref Unsafe.Add(ref currentRow, j - 1 - jLength))
                    + Unsafe.As<byte, Vector<byte>>(ref Unsafe.Add(ref currentRow, j - jLength))
                    + Unsafe.As<byte, Vector<byte>>(ref Unsafe.Add(ref currentRow, j + 1 - jLength))
                    + Unsafe.As<byte, Vector<byte>>(ref Unsafe.Add(ref currentRow, j - 1))
                    + Unsafe.As<byte, Vector<byte>>(ref Unsafe.Add(ref currentRow, j + 1))
                    + Unsafe.As<byte, Vector<byte>>(ref Unsafe.Add(ref currentRow, j - 1 + jLength))
                    + Unsafe.As<byte, Vector<byte>>(ref Unsafe.Add(ref currentRow, j + jLength))
                    + Unsafe.As<byte, Vector<byte>>(ref Unsafe.Add(ref currentRow, j + 1 + jLength));

                Unsafe.As<byte, Vector<byte>>(
                    ref Unsafe.Add(ref currentResultRow, j)) = Vector.Negate(
                    Vector.ConditionalSelect(
                        Vector.Negate(Unsafe.As<byte, Vector<byte>>(
                            ref Unsafe.Add(ref currentRow, j))),
                        Vector.BitwiseAnd(
                            Vector.GreaterThan(neighbours, new Vector<byte>(3)),
                            Vector.LessThan(neighbours, new Vector<byte>(6))
                        ),
                        Vector.BitwiseAnd(
                            Vector.GreaterThan(neighbours, Vector<byte>.One),
                            Vector.LessThan(neighbours, new Vector<byte>(5))
                        )
                    )
                );
            }
            {
                Vector<byte> neighbours
                    = Unsafe.As<byte, Vector<byte>>(ref Unsafe.Add(ref currentRow, j - 1 - jLength))
                    + Unsafe.As<byte, Vector<byte>>(ref Unsafe.Add(ref currentRow, j - jLength))
                    + Unsafe.As<byte, Vector<byte>>(ref Unsafe.Add(ref currentRow, j + 1 - jLength))
                    + Unsafe.As<byte, Vector<byte>>(ref Unsafe.Add(ref currentRow, j - 1))
                    + Unsafe.As<byte, Vector<byte>>(ref Unsafe.Add(ref currentRow, j + 1))
                    + Unsafe.As<byte, Vector<byte>>(ref Unsafe.Add(ref currentRow, j - 1 + jLength))
                    + Unsafe.As<byte, Vector<byte>>(ref Unsafe.Add(ref currentRow, j + jLength))
                    + Unsafe.As<byte, Vector<byte>>(ref Unsafe.Add(ref currentRow, j + 1 + jLength));
                Unsafe.As<byte, Vector<byte>>(
                    ref Unsafe.Add(ref currentResultRow, j)) = Vector.Negate(
                    Vector.BitwiseAnd(
                        Vector.ConditionalSelect(
                            Vector.Negate(Unsafe.As<byte, Vector<byte>>(ref Unsafe.Add(ref currentRow, j))),
                            Vector.BitwiseAnd(
                                Vector.GreaterThan(neighbours, new Vector<byte>(3)),
                                Vector.LessThan(neighbours, new Vector<byte>(6))
                            ),
                            Vector.BitwiseAnd(
                                Vector.GreaterThan(neighbours, Vector<byte>.One),
                                Vector.LessThan(neighbours, new Vector<byte>(5))
                            )
                        ),
                        new Vector<byte>(mask)
                    )
                );
            }
        }
        mr[startingPoint.i, startingPoint.j] = 0;
        mr[endingPoint.i, endingPoint.j] = 0;
    }

    static byte[,] ParseFile(string filePath)
    {
        var file = File.ReadAllLines(filePath);
        int rows = file.Length;
        int columns = file.First().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        //byte[,] m = new byte[rows + 2, columns + 2];
        mask = new byte[Vector<byte>.Count];
        mask.AsSpan(0, columns % Vector<byte>.Count).Fill(255);

        byte[,] m = new byte[rows + 2, columns + 2 + Vector<byte>.Count - columns % Vector<byte>.Count];
        startingBound = (1, 1);
        endingBound = (rows, columns);
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
