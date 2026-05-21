using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Sickle.Heart.Core;

public static class Resources {
    
    private static readonly Dictionary<string, object> Library = [];
    private static readonly Dictionary<string, string[]> FileLists = [];
    
    public static string FindResourceFile(params string[] relativePathParts) {

        relativePathParts = relativePathParts.Prepend("Resources").ToArray();
        
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null) {

            var candidate = Path.Combine([current.FullName, .. relativePathParts]);

            if (File.Exists(candidate))
                return candidate;

            current = current.Parent;
        }

        throw new FileNotFoundException($"Resource not found: {Path.Combine(relativePathParts)}");
    }

    public static T GetResource<T>(params string[] relativePathParts) {
        
        var path = FindResourceFile(relativePathParts);

        if (typeof(T) == typeof(Texture2D)) {

            if (Library.TryGetValue(path, out var resource))
                return (T)resource;

            Library[path] = LoadTexture(path);
            return (T)Library[path];
        }

        throw new FileNotFoundException($"Resource not found: {path}");
    }

    public static string[] GetResourceFiles(params string[] relativePathParts) {

        var directory = FindResourceDirectory(relativePathParts);

        if (FileLists.TryGetValue(directory, out var files))
            return files;

        files = Directory.GetFiles(directory)
            .Select(Path.GetFileName)
            .Where(file => !string.IsNullOrWhiteSpace(file))
            .Cast<string>()
            .OrderBy(file => file)
            .ToArray();

        FileLists[directory] = files;
        return files;
    }

    public static string FindResourceDirectory(params string[] relativePathParts) {

        relativePathParts = relativePathParts.Prepend("Resources").ToArray();

        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null) {

            var candidate = Path.Combine([current.FullName, .. relativePathParts]);

            if (Directory.Exists(candidate))
                return candidate;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException($"Resource directory not found: {Path.Combine(relativePathParts)}");
    }
}
