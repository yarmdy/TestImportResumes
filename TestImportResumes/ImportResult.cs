public class ImportResult
{
    public static ImportResult Success(string source, IDictionary<string, string> result)
    {
        ImportResult tresult = new ImportResult(source) { Result = result };
        return tresult;
    }
    public static ImportResult Error(string source, Exception ex)
    {
        ImportResult tresult = new ImportResult(source) { Exception = ex };
        return tresult;
    }
    private ImportResult(string source)
    {
        Source = source;
    }
    public string Source { get; }
    public IDictionary<string, string>? Result { get; init; }
    public Exception? Exception { get; init; }
}
