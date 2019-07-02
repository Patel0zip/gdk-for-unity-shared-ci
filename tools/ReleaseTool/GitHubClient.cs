﻿using CommandLine;
using Octokit;
using System;
using System.IO;
using System.Text.RegularExpressions;
using OctoClient = Octokit.GitHubClient;

namespace ReleaseTool
{
    /// <summary>
    /// Wrapper around Octokit's GitHubClient, which provides a synchronous interface to access GitHub.
    /// </summary>
    internal class GitHubClient
    {
        private const string DefaultKeyFileLocation = "~/.ssh/github.token";

        private const string RemoteUrlRegex = @"(?:https:\/\/github\.com\/|git@github\.com\:)([^\/\.]*)\/([^\/\.]*)\.git";

        private static readonly ProductHeaderValue ProductHeader = new ProductHeaderValue("improbable-unity-gdk-release-tool");

        public interface IGitHubOptions
        {
            [Option("github-key-file", Default = DefaultKeyFileLocation, HelpText = "The location of the github token file.")]
            string GitHubTokenFile { get; set; }

            [Option("github-key", HelpText = "The github API token. If this is set, this will override the " +
                                             "github-key-file.")]
            string GitHubToken { get; set; }
        }

        private readonly IGitHubOptions options;
        private readonly OctoClient octoClient;

        public GitHubClient(IGitHubOptions options)
        {
            octoClient = new OctoClient(ProductHeader);
            this.options = options;
        }
            
        public void LoadCredentials()
        {
            if (!string.IsNullOrEmpty(options.GitHubToken))
            {
                octoClient.Credentials = new Credentials(options.GitHubToken);
            }
            else
            {
                if (!File.Exists(options.GitHubTokenFile))
                {
                    throw new ArgumentException("Failed to get GitHub Token as the file specified does not exist.");
                }
                
                octoClient.Credentials = new Credentials(File.ReadAllText(
                    Common.ReplaceHomePath(options.GitHubTokenFile)));
            }
        }

        public Repository GetRepositoryFromRemote(string remote)
        {
            var matches = Regex.Match(remote, RemoteUrlRegex);

            if (!matches.Success)
            {
                throw new ArgumentException($"Failed to parse remote {remote}. Not a valid github repository.");
            }

            var owner = matches.Groups[1].Value;
            var repo = matches.Groups[2].Value;

            var repositoryTask = octoClient.Repository.Get(owner, repo);

            return repositoryTask.Result;
        }

        public PullRequest CreatePullRequest(Repository repository, string branchFrom, string branchTo, string pullRequestTitle)
        { 
            var newPullRequest = new NewPullRequest(pullRequestTitle, branchFrom, branchTo);
            var createPullRequestTask = octoClient.PullRequest.Create(repository.Id, newPullRequest);

            return createPullRequestTask.Result;
        }

        public PullRequestMerge MergePullRequest(Repository repository, int pullRequestId)
        {
            var mergePullRequest = new MergePullRequest
            {
                MergeMethod = PullRequestMergeMethod.Squash
            };

            var mergePullRequestTask = octoClient.PullRequest.Merge(repository.Id, pullRequestId, mergePullRequest);

            return mergePullRequestTask.Result;
        }

        public Release CreateDraftRelease(Repository repository, string tag, string body, string name, string commitish)
        {
            var releaseTask = octoClient.Repository.Release.Create(repository.Id, new NewRelease(tag)
            {
                Body = body,
                Draft = true,
                Name = name,
                TargetCommitish = commitish
            });

            return releaseTask.Result;
        }

        public ReleaseAsset AddAssetToRelease(Release release, string fileName, string contentType, Stream data)
        {
            var uploadAssetTask = octoClient.Repository.Release.UploadAsset(release, new ReleaseAssetUpload(fileName, contentType, data, null));
            return uploadAssetTask.Result;
        }
    }
}
