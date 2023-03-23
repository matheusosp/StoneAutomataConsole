using Microsoft.VisualBasic;

string basePath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
Console.WriteLine(	
	FindPath(Path.Combine(basePath, "input.txt"))
);

string FindPath(string filePath)
{
    var file = File.ReadAllLines(filePath);
    int rows = file.Length;
    int columns = file.First().Split(' ').Length;
    int[,] m = new int[rows, columns];
    for (int i = 0; i < rows; i++)
    {
        string[] data = file[i].Split(' ');
        for (int j = 0; j < columns; j++)
            m[i, j] = int.Parse(data[j]);
    }
    int[,] mr = new int[rows, columns];
    m[0, 0] = 0;
    m[rows - 1, columns - 1] = 0;

    Context found = null;
    var context = new Context(null, ' ', 0, 0);
    HashSet<Context> contexts = new HashSet<Context> {
        context
    };

    List<Context> toBeRemoved = new List<Context>();
    HashSet<Context> toBeAdded = new HashSet<Context>();

    var final = new Context(null, ' ', rows - 1, columns - 1);
    //Console.WriteLine("Original");
    //Write(m);
    int iterations = 0;
    for (int i = 0; i < int.MaxValue; i++)
    {
        NextGen(m, mr);
        foreach (var element in contexts)
        {
            toBeRemoved.Add(element);
            foreach (var possiblePath in PossiblePaths(element, mr))
            {
                if (!toBeAdded.Contains(possiblePath))
                    toBeAdded.Add(possiblePath);
                iterations++;
            }
        }
        foreach (var element in toBeRemoved)
            contexts.Remove(element);
        toBeRemoved.Clear();
        foreach (var element in toBeAdded)
            contexts.Add(element);
        toBeAdded.Clear();
        if (contexts.TryGetValue(final, out found))
        {
            Console.WriteLine("Found solution");
            break;
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
    Console.WriteLine("Iterations {0}", iterations);
    return WriteContext(found);
}
string WriteContext(Context found)
{
    Stack<Context> reversed = new Stack<Context>();
    do
    {
        reversed.Push(found);
        found = found.parent;
    } while (found != null);
    return string.Join(' ', reversed.Skip(1).Select(c => c.direction).ToArray());
}
void Write(int[,] m)
{
    int iUpperBound = m.GetUpperBound(0);
    int jUpperBound = m.GetUpperBound(1);

    for (int i = 0; i <= iUpperBound; i++)
	{
        for (int j = 0; j <= jUpperBound; j++)
		{
			Console.Write(m[i, j]);
		}
        Console.WriteLine();
    }
    Console.WriteLine();
}


IEnumerable<Context> PossiblePaths(Context context, int[,] m)
{
	int i = context.i;
	int j = context.j;

	int iUpperBound = m.GetUpperBound(0);
	int jUpperBound = m.GetUpperBound(1);
    
	if (i > 0 && m[i - 1, j] == 0)
	{
		yield return new Context(context, 'U', i - 1, j);
	}
	if (j > 0 && m[i, j - 1] == 0)
	{
		yield return new Context(context, 'L', i, j - 1);
	}
	if (i < iUpperBound && m[i + 1, j] == 0)
	{
		yield return new Context(context, 'D', i + 1, j);
	}
	if (j < jUpperBound && m[i, j + 1] == 0)
	{
		yield return new Context(context, 'R', i, j + 1);
	}
}

void Exchange(ref int[,] m, ref int[,] mr)
{
	var tmp = m;
	m = mr;
	mr = tmp;
}
void NextGen(int[,] m, int[,] mr)
{
	int iUpperBound = m.GetUpperBound(0);
	int jUpperBound = m.GetUpperBound(1);
	for (int i = 0; i <= iUpperBound; i++)
	{
		for (int j = 0; j <= jUpperBound; j++)
		{
			int score = 0;
			if (i > 0)
			{
				score += m[i - 1, j];
				if (j > 0)
					score += m[i - 1, j - 1];
			}
			if (j > 0)
			{
				score += m[i, j - 1];
				if (i < iUpperBound)
					score += m[i + 1, j - 1];
			}
			if (i < iUpperBound)
			{
				score += m[i + 1, j];
				if (j < jUpperBound)
					score += m[i + 1, j + 1];
			}
			if (j < jUpperBound)
			{
				score += m[i, j + 1];
				if (i > 0)
					score += m[i - 1, j + 1];
			}
			if (m[i, j] == 1)
				mr[i,j] = score > 3 && score < 6 ? 1 : 0;
			else
				mr[i, j] = score > 1 && score < 5 ? 1 : 0;
		}
	}
	mr[0, 0] = 0;
	mr[iUpperBound, jUpperBound] = 0;
}
public class Context : IEquatable<Context>
{
    public Context parent;
    public char direction;
    public int i;
    public int j;
    public Context(Context parent, char direction, int i, int j)
    {
        this.parent = parent;
        this.direction = direction;
        this.i = i;
        this.j = j;
    }

    public bool Equals(Context other)
    {
        return i == other.i
        && j == other.j;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(i, j);
    }
}