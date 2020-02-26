using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using CommandLine;
using Octokit;

namespace ReleaseTool
{
    /// <summary>
    ///     Runs the commands required for releasing a candidate.
    ///     * Does a final bump of the gdk.pinned (if needed).
    ///     * Merges the candidate branch into develop.
    ///     * Pushes develop to origin/develop and origin/master.
    ///     * Creates a GitHub release draft.
    /// </summary>
    internal class ReleaseCommand
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private const string PackageContentType = "application/zip";
        private const string ChangeLogFilename = "CHANGELOG.md";

        [Verb("release", HelpText = "Merge a release branch and create a github release draft.")]
        public class Options : GitHubClient.IGitHubOptions, BuildkiteMetadataSink.IBuildkiteOptions
        {
            [Value(0, MetaName = "version", HelpText = "The version that is being released.")]
            public string Version { get; set; }

            [Option('u', "pull-request-url", HelpText = "The link to the release candidate branch to merge.",
                Required = true)]
            public string PullRequestUrl { get; set; }

            public string GitHubTokenFile { get; set; }

            public string GitHubToken { get; set; }

            public string MetadataFilePath { get; set; }
        }

        private readonly Options options;

        public ReleaseCommand(Options options)
        {
            this.options = options;
        }

        /*
         *     This tool is designed to execute most of the physical releasing:
         *         1. Merge the RC PR into develop.
         *         2. Draft the release using the changelog notes.
         *
         *     Fast-forwarding master up-to-date with develop and publishing the release
         *     are left up to the release sheriff.
         */
        public int Run()
        {
            try
            {
                var gitHubClient = new GitHubClient(options);

                var (repoName, pullRequestId) = ExtractPullRequestInfo(options.PullRequestUrl);

                var spatialOsRemote = string.Format(Common.RemoteUrlTemplate, Common.SpatialOsOrg, repoName);
                var gitHubRepo = gitHubClient.GetRepositoryFromRemote(spatialOsRemote);

                // Merge into develop
                var mergeResult = gitHubClient.MergePullRequest(gitHubRepo, pullRequestId);

                if (!mergeResult.Merged)
                {
                    throw new InvalidOperationException(
                        $"Was unable to merge pull request at: {options.PullRequestUrl}. Received error: {mergeResult.Message}");
                }

                // Delete remote on the forked repository.
                var forkedRepoRemote = string.Format(Common.RemoteUrlTemplate, Common.GithubBotUser, repoName);
                var branchName = string.Format(Common.ReleaseBranchNameTemplate, options.Version);
                // gitHubClient.DeleteBranch(gitHubClient.GetRepositoryFromRemote(forkedRepoRemote), branchName);

                var remoteUrl = string.Format(Common.RemoteUrlTemplate, Common.SpatialOsOrg, repoName);

                using (var gitClient = GitClient.FromRemote(remoteUrl))
                {
                    // Create release
                    gitClient.Fetch();
                    gitClient.CheckoutRemoteBranch(Common.DevelopBranch);
                    var release = CreateRelease(gitHubClient, gitHubRepo, gitClient, repoName);

                    Logger.Info("Release Successful!");
                    Logger.Info("Release hash: {0}", gitClient.GetHeadCommit().Sha);
                    Logger.Info("Draft release: {0}", release.HtmlUrl);

                    if (repoName == "gdk-for-unity" && BuildkiteMetadataSink.CanWrite(options))
                    {
                        using (var sink = new BuildkiteMetadataSink(options))
                        {
                            sink.WriteMetadata("gdk-for-unity-hash", gitClient.GetHeadCommit().Sha);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "ERROR: Unable to release candidate branch. Error: {0}", e);
                return 1;
            }

            return 0;
        }

        private Release CreateRelease(GitHubClient gitHubClient, Repository gitHubRepo, GitClient gitClient, string repoName)
        {
            var headCommit = gitClient.GetHeadCommit().Sha;

            string changelog;
            using (new WorkingDirectoryScope(gitClient.RepositoryPath))
            {
                changelog = GetReleaseNotesFromChangeLog();
            }

            string name;
            string preamble;

            switch (repoName)
            {
                case "gdk-for-unity":
                    name = $"GDK for Unity Alpha Release {options.Version}";
                    preamble =
@"In this release, we've ...

We've also fixed ... 

Keep giving us your feedback and/or suggestions! Check out [our Discord](https://discord.gg/SCZTCYm), [our forums](https://forums.improbable.io/), or here in the [Github issues](https://github.com/spatialos/gdk-for-unity/issues?q=is%3Aissue+is%3Aopen+sort%3Aupdated-desc)!

See the full release notes below! 👇";
                    break;
                case "gdk-for-unity-fps-starter-project":
                    name = $"GDK for Unity FPS Starter Project Alpha Release {options.Version}";
                    preamble =
$@"This release of the FPS Starter Project is intended for use with the GDK for Unity Alpha Release {options.Version}.

Keep giving us your feedback and/or suggestions! Check out [our Discord](https://discord.gg/SCZTCYm), [our forums](https://forums.improbable.io/), or here in the [Github issues](https://github.com/spatialos/gdk-for-unity/issues?q=is%3Aissue+is%3Aopen+sort%3Aupdated-desc)!";
                    break;
                case "gdk-for-unity-blank-project":
                    name = $"GDK for Unity Blank Project Alpha Release {options.Version}";
                    preamble =
$@"This release of the Blank Project is intended for use with the GDK for Unity Alpha Release {options.Version}.

Keep giving us your feedback and/or suggestions! Check out [our Discord](https://discord.gg/SCZTCYm), [our forums](https://forums.improbable.io/), or here in the [Github issues](https://github.com/spatialos/gdk-for-unity/issues?q=is%3Aissue+is%3Aopen+sort%3Aupdated-desc)!";
                    break;
                default:
                    throw new ArgumentException("Unsupported repository.", nameof(repoName));
            }

            var releaseBody =
$@"{preamble}

---

{changelog}";

            return gitHubClient.CreateDraftRelease(gitHubRepo, options.Version, releaseBody, name, headCommit);
        }

        private static (string, int) ExtractPullRequestInfo(string pullRequestUrl)
        {
            const string regexString = "github\\.com\\/spatialos\\/(.*)\\/pull\\/([0-9]*)";

            var match = Regex.Match(pullRequestUrl, regexString);

            if (!match.Success)
            {
                throw new ArgumentException($"Malformed pull request url: {pullRequestUrl}");
            }

            if (match.Groups.Count < 3)
            {
                throw new ArgumentException($"Malformed pull request url: {pullRequestUrl}");
            }

            var repoName = match.Groups[1].Value;
            var pullRequestIdStr = match.Groups[2].Value;

            if (!int.TryParse(pullRequestIdStr, out int pullRequestId))
            {
                throw new Exception(
                    $"Parsing pull request URL failed. Expected number for pull request id, received: {pullRequestIdStr}");
            }

            return (repoName, pullRequestId);
        }

        private static string GetReleaseNotesFromChangeLog()
        {
            if (!File.Exists(ChangeLogFilename))
            {
                throw new InvalidOperationException("Could not get draft release notes, as the change log file, " +
                    $"{ChangeLogFilename}, does not exist.");
            }

            Logger.Info("Reading {0}...", ChangeLogFilename);

            var releaseBody = new StringBuilder();
            var changedSection = 0;

            using (var reader = new StreamReader(ChangeLogFilename))
            {
                while (!reader.EndOfStream)
                {
                    // Here we target the second Heading2 ("##") section.
                    // The first section will be the "Unreleased" section. The second will be the correct release notes.
                    var line = reader.ReadLine();
                    if (line.StartsWith("## "))
                    {
                        changedSection += 1;

                        if (changedSection == 3)
                        {
                            break;
                        }

                        continue;
                    }

                    if (changedSection == 2)
                    {
                        releaseBody.AppendLine(line);
                    }
                }
            }

            return releaseBody.ToString();
        }
    }
}
