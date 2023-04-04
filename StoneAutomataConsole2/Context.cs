public class Context : IEquatable<Context>
{
    public readonly Context? Parent;
    public readonly char Direction;
    public readonly int I;
    public readonly int J;
    public readonly int Offset;
    public Context(char direction, int i, int j, int offset)
        : this(null, direction, i, j, offset)
    { 
    }
    public Context(Context? parent, char direction, int i, int j, int offset)
    {
        this.Parent = parent;
        this.Direction = direction;
        this.I = i;
        this.J = j;
        this.Offset = offset;
    }

    public bool Equals(Context other)
    {
        return Offset == other.Offset;
    }

    public override int GetHashCode()
    {
        return Offset;
    }
}
