internal class ContextStatistics
{
    public readonly List<string> Paths;
    public readonly List<byte[,]> Boards;
    public ContextStatistics()
    {
        Paths = new List<string>();
        this.Boards = new List<byte[,]>();
    }

}