namespace Content.Shared._Starlight;

public static class ListExtensions
{
    public static void RemoveSwapBack<T>(this List<T> list, int index)
    {
        var last = list.Count - 1;
        list[index] = list[last];
        list.RemoveAt(last);
    }
}
