public class Context : IEquatable<Context>
{
    public readonly Context? Parent;
    public readonly char Direction;
    public readonly int I;
    public readonly int J;
    public readonly int Offset;
    public readonly int Lives;
    public byte[,]? Matrix;
    public byte[,]? MatrixNext;
    public Context(char direction, int i, int j, int offset, byte[,] matrix)
        : this(null, direction, i, j, offset, 31, matrix)
    {
    }
    public Context(Context? parent, char direction, int i, int j, int offset, int lives, byte[,] matrix)
    {
        this.Parent = parent;
        this.Direction = direction;
        this.I = i;
        this.J = j;
        this.Offset = offset;
        this.Lives = lives;
        this.Matrix = matrix;
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
