﻿using System;
using System.IO;

namespace ReleaseTool
{
    internal static class Common
    {
        public static string ReplaceHomePath(string originalPath)
        {
            if (!originalPath.StartsWith("~"))
            {
                return originalPath;
            }

            var relativePath = new Uri(originalPath.Substring(2), UriKind.Relative);
            var homeDirectory = AppendDirectorySeparator(Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile));
            var homePath = new Uri(homeDirectory, UriKind.Absolute);

            return new Uri(homePath, relativePath).AbsolutePath;
        }

        /**
         * This is required as URIs treat paths that do not have a trailing slash as a file rather than a directory.
         */
        public static string AppendDirectorySeparator(string originalPath)
        {
            var directorySeparator = Path.DirectorySeparatorChar.ToString();
            var altDirectorySeparator = Path.AltDirectorySeparatorChar.ToString();

            if (originalPath.EndsWith(directorySeparator) || originalPath.EndsWith(altDirectorySeparator))
            {
                return originalPath;
            }

            if (originalPath.Contains(altDirectorySeparator))
            {
                return originalPath + altDirectorySeparator;
            }

            return originalPath + directorySeparator;
        }
    }
}
