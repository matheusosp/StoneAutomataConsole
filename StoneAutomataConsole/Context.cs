public class Context : IEquatable<Context>
{
    public Context? parent;
    public char direction;
    public int i;
    public int j;
    public Context(Context? parent, char direction, int i, int j)
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
