public interface IDicToObjConverter
{
    T Convert<T>(IDictionary<string, object?> dic) where T : class, new();
}
