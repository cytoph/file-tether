namespace FileTether.Core.Tethering.Internal;

public static class PathExpander
{
    public static string Expand(string path)
    {
        if (path == "~" || path.StartsWith("~/") || path.StartsWith(@"~\"))
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            path = path.Length == 1 ? home : Path.Combine(home, path[2..]);
        }

        return Path.GetFullPath(path);
    }
}
