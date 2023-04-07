public class Context : IEquatable<Context>
{
    public readonly Context? Parent;
    public readonly char Direction;
    public readonly int I;
    public readonly int J;
    public readonly int Offset;
    public readonly int Generation;
    public byte[,] Board;

    public Context(int generation, char direction, int i, int j, int offset)
        : this(null, direction, i, j, offset)
    {
        this.Generation = generation;
    }
    public Context(Context? parent, char direction, int i, int j, int offset)
    {
        if(parent != null)
           this.Generation = parent.Generation;
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
