using System;
using System.IO;

namespace Oproto.Lambda.OpenApi.Merge;

/// <summary>
/// Utility for expanding paths with tilde notation.
/// </summary>
public static class PathExpander
{
    /// <summary>
    /// Expands a path that may contain tilde notation.
    /// </summary>
    /// <param name="path">The path to expand.</param>
    /// <returns>The expanded path.</returns>
    /// <exception cref="ArgumentException">Thrown when tilde expansion fails.</exception>
    public static string ExpandPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        if (!path.StartsWith("~"))
            return path;

        // Handle ~/path (current user's home)
        if (path == "~" || path.StartsWith("~/") || path.StartsWith("~\\"))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(home))
                throw new ArgumentException($"Cannot expand path '{path}': Unable to determine home directory.");

            if (path == "~")
                return home;

            // Skip the ~/ or ~\ prefix
            var relativePath = path.Substring(2);
            return Path.Combine(home, relativePath);
        }

        // Handle ~username/path (Unix-style, other user's home)
        // On Windows, we don't support ~username syntax
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            throw new ArgumentException($"Cannot expand path '{path}': ~username syntax is not supported on Windows.");

        // Extract username from path
        var slashIndex = path.IndexOfAny(new[] { '/', '\\' });
        var username = slashIndex > 0 ? path.Substring(1, slashIndex - 1) : path.Substring(1);

        if (string.IsNullOrEmpty(username))
            throw new ArgumentException($"Cannot expand path '{path}': Invalid tilde syntax.");

        // On Unix, try to resolve the user's home directory
        var userHome = ResolveUserHomeDirectory(username);
        if (userHome == null)
            throw new ArgumentException($"Cannot expand path '{path}': User '{username}' not found.");

        return slashIndex > 0
            ? Path.Combine(userHome, path.Substring(slashIndex + 1))
            : userHome;
    }

    /// <summary>
    /// Resolves the home directory for a given username on Unix systems.
    /// </summary>
    private static string? ResolveUserHomeDirectory(string username)
    {
        // Try /home/username (Linux)
        var linuxHome = $"/home/{username}";
        if (Directory.Exists(linuxHome))
            return linuxHome;

        // Try /Users/username (macOS)
        var macHome = $"/Users/{username}";
        if (Directory.Exists(macHome))
            return macHome;

        return null;
    }
}
