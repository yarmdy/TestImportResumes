public class CanImportResult:IAsyncDisposable
{
    public static CanImportResult Error(Exception ex)
    {
        return new CanImportResult {Result=false,Exception=ex };
    }
    public static CanImportResult Success()
    {
        return new CanImportResult { Result=true};
    }

    public ValueTask DisposeAsync()
    {
        throw new NotImplementedException();
    }

    public bool Result { get; init; }
    public Exception? Exception { get; init; }
}
