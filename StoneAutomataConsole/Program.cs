﻿using System.Diagnostics;
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
        string filePath = Path.Combine(basePath, "input3.txt");
        string result = null;
        Stopwatch sw = new Stopwatch();
        long sum = 0;
        long min = long.MaxValue;
        long max = long.MinValue;
        int loop = 1;
        byte[,] m = null;
        for (int i = 0; i < loop; i++)
        {
            m = ParseFile(filePath);
            GC.Collect();
            sw.Restart();
            result = FindPath(m);
            sw.Stop();
            sum += sw.ElapsedTicks;
            min = long.Min(min, sw.ElapsedTicks);
            max = long.Max(max, sw.ElapsedTicks);
        }
        Console.WriteLine($"Size: {endingBound.i + 1}x{endingBound.j + 1}");
        Console.WriteLine($"Avg: {TimeSpan.FromTicks(sum / loop)}, Min: {TimeSpan.FromTicks(min)}, Max: {TimeSpan.FromTicks(max)}");
        Console.WriteLine($"Steps ({result.Split(' ').Length}): {result}");
    }

    static string FindPath(byte[,] m)
    {
        string last = null;
        int solutions = 0;
        int iLength = m.GetUpperBound(0) + 1;
        int jLength = m.GetUpperBound(1) + 1;

        m[startingPoint.i, startingPoint.j] = 0;
        m[endingPoint.i, endingPoint.j] = 0;

        List<Context> contexts = new List<Context>(m.Length / 4)
        { 
            new Context(' ', startingPoint.i, startingPoint.j, startingPoint.i * jLength + startingPoint.j)
        };

        //ConcurrentDictionary<int, Context> toBeAdded = new ConcurrentDictionary<int, Context>(3, m.Length);
        Context?[] toBeAdded = new Context?[m.Length];
        List<int> toBeAddedIndex = new List<int>(m.Length);

        byte[,] mr = new byte[iLength, jLength];
        var final = new Context(' ', endingPoint.i, endingPoint.j, endingPoint.i * jLength + endingPoint.j);
        //var source = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        //int step = 0;
        while (true)
        {
            NextGen(m, mr, jLength);
            //Parallel.ForEach(contexts, new ParallelOptions { MaxDegreeOfParallelism = 4 }, element =>
            var smr = MemoryMarshal.CreateSpan(ref mr[0, 0], mr.Length);

            var s = CollectionsMarshal.AsSpan(contexts);
            for (int i = 0; i < s.Length; i++)
            {
                var element = s[i];
                // Current path is replaced by new paths
                AddPossiblePaths(element, smr, toBeAdded, toBeAddedIndex, jLength);
            }
            //);
            // if touched final destination, solution is found
            if (toBeAdded[final.Offset] is Context found)
            {
                return ExtractSteps(found);
                //if(source.IsCancellationRequested)
                //{
                //    last = ExtractSteps(found);
                //    Console.WriteLine($"Solution: {last} ({last.Length})");
                //    source = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                //}
            }
            // Remove deadends and replaced paths
            contexts.Clear();

            //step++;
            // Add new paths
            foreach (var index in toBeAddedIndex)
            {
                //Console.WriteLine($"Step: {step} - {ExtractSteps(element.Value)}");
                var element = toBeAdded[index];
                contexts.Add(element);
                toBeAdded[index] = null;
            }
            toBeAddedIndex.Clear();
            

            // No more paths to take
            if (contexts.Count == 0)
            {
                if (last != null)
                    return last;
                else
                {
                    Console.WriteLine("Impossible maze");
                    return "";
                }
            }
            Swap(ref m, ref mr);
        }
    }
    static string ExtractSteps(Context? found)
    {
        Stack<Context> reversed = new Stack<Context>(512);
        while (found != null)
        {
            reversed.Push(found);
            found = found.Parent;
        }
        var result = reversed.Skip(1).Select(c => c.Direction).ToArray();
        return string.Join(' ', result);
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
