public class ImportResult
{
    public static ImportResult Success(string source, ZZ_XQ_Resumes_Entity result)
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
    public ZZ_XQ_Resumes_Entity? Result { get; init; }
    public Exception? Exception { get; init; }
}


