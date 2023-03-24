using System.Diagnostics;

internal class Program
{
    private static (int i, int j) startingPoint;
    private static (int i, int j) endingPoint;

    private static void Main(string[] args)
    {
        string basePath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            Console.WriteLine(
            FindPath(Path.Combine(basePath, "input.txt"))
            );
        }
    static string FindPath(string filePath)
    {
        byte[,] m = ParseFile(filePath);
        int rows = m.GetUpperBound(0) + 1;
        int columns = m.GetUpperBound(1) + 1;
        byte[,] mr = new byte[rows, columns];

        m[startingPoint.i, startingPoint.j] = 0;
        m[endingPoint.i, endingPoint.j] = 0;

        Context? found = null;
        HashSet<Context> contexts = new HashSet<Context>(m.Length) {
            new Context(null, ' ', startingPoint.i, startingPoint.j)
        };

        List<Context> toBeRemoved = new List<Context>(m.Length);
        Dictionary<int, Context> toBeAdded = new Dictionary<int, Context>(m.Length);

        var final = new Context(null, ' ', endingPoint.i, endingPoint.j);
        //Console.WriteLine("Original");
        //Write(m);
        Context[] directionsPreAllocated = new Context[4];
        int iUpperBound = m.GetUpperBound(0);
        int jUpperBound = m.GetUpperBound(1);

        int iterations = 0;
        for (int i = 0; i < int.MaxValue; i++)
        {
            NextGen(m, mr, iUpperBound, jUpperBound);
            foreach (var element in contexts)
            {
                toBeRemoved.Add(element);
                foreach (var possiblePath in PossiblePaths(element, mr, directionsPreAllocated,
                    (i, j) => !toBeAdded.ContainsKey(HashCode.Combine(i, j)), iUpperBound, jUpperBound))
                {
                    toBeAdded.Add(possiblePath.GetHashCode(), possiblePath);
                    iterations++;
                }
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
                return WriteContext(found);
            }
            if (contexts.Count == 0)
            {
                Console.WriteLine("Sem caminho possível");
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

            Exchange(ref m, ref mr);
        }
        Console.WriteLine("Caminho longe demais");
        return "";
    }
    static string WriteContext(Context found)
    {
        Stack<Context> reversed = new Stack<Context>();
        do
        {
            reversed.Push(found);
            found = found.parent;
        } while (found != null);
        return string.Join(' ', reversed.Skip(1).Select(c => c.direction).ToArray());
    }

    static Span<Context> PossiblePaths(Context context, byte[,] m, Context[] directions, Func<int, int, bool> shouldCreate, int iUpperBound, int jUpperBound)
    {
        int count = 0;
        int i = context.i;
        int j = context.j;

        if (i > 0 && m[i - 1, j] == 0 && shouldCreate(i - 1, j))
            directions[count++] = new Context(context, 'U', i - 1, j);
        if (j > 0 && m[i, j - 1] == 0 && shouldCreate(i, j - 1))
            directions[count++] = new Context(context, 'L', i, j - 1);
        if (i < iUpperBound && m[i + 1, j] == 0 && shouldCreate(i + 1, j))
            directions[count++] = new Context(context, 'D', i + 1, j);
        if (j < jUpperBound && m[i, j + 1] == 0 && shouldCreate(i, j + 1))
            directions[count++] = new Context(context, 'R', i, j + 1);
        return directions.AsSpan(0, count);
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
            SetGeneration(m, mr, i, 0, 
                SlowPathScore(m, i, 0, iUpperBound, jUpperBound));
            SetGeneration(m, mr, i, jUpperBound,
                SlowPathScore(m, i, jUpperBound, iUpperBound, jUpperBound));
        }
        for (int j = 0; j <= jUpperBound; j++)
        {
            SetGeneration(m, mr, 0, j,
                SlowPathScore(m, 0, j, iUpperBound, jUpperBound));
            SetGeneration(m, mr, iUpperBound, j,
                SlowPathScore(m, iUpperBound, j, iUpperBound, jUpperBound));
        }
        //for (int i = 1; i < iUpperBound; i++)
        Parallel.For(1, iUpperBound, i =>
        { 
            for (int j = 1; j < jUpperBound; j++)
            {
                SetGeneration(m, mr, i, j, 
                    m[i - 1, j - 1]
                    + m[i - 1, j]
                    + m[i - 1, j + 1]
                    + m[i, j - 1]
                    + m[i, j + 1]
                    + m[i + 1, j - 1]
                    + m[i + 1, j]
                    + m[i + 1, j + 1]);
            }
        }
        );
        mr[startingPoint.i, startingPoint.j] = 0;
        mr[endingPoint.i, endingPoint.j] = 0;
    }

    private static void SetGeneration(byte[,] m, byte[,] mr, int i, int j, int score)
    {
        if (m[i, j] == 1)
            mr[i, j] = score > 3 && score < 6 ? (byte)1 : (byte)0;
        else
            mr[i, j] = score > 1 && score < 5 ? (byte)1 : (byte)0;
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
        byte value = m[i, j];
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
