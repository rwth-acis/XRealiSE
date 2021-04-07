#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Octokit;
using Octokit.Internal;
using XRealiSE_Crawler.KeywordExtractor;
using XRealiSE_DBConnection;
using XRealiSE_DBConnection.data;
using static System.Console;
using Range = Octokit.Range;

#endregion

namespace XRealiSE_Crawler
{
    internal class Crawler
    {
        private readonly DatabaseConnection _databaseConnection;
        private readonly GitHubClient _gitHubClient;
        private readonly Dictionary<KeywordInRepository.KeywordInRepositoryType, BasicExtractor> _keywordExtractors;
        private readonly RepositoryContentsClient _repositoryContentsClient;
        private List<long> _crawledRepos;
        private int _rateLimit;
        private int _searchLimit;

        public Crawler(string apikey, string mySqlHost, string mySqlUsername, string mySqlPassword,
            string mySqlDatabase, uint mySqlPort)
        {
            ProductHeaderValue productHeaderValue = new ProductHeaderValue("xrealise");
            _gitHubClient = new GitHubClient(productHeaderValue, new InMemoryCredentialStore(new Credentials(apikey)));
            _repositoryContentsClient =
                new RepositoryContentsClient(
                    new ApiConnection(new Connection(productHeaderValue,
                        new InMemoryCredentialStore(new Credentials(apikey)))));
            _keywordExtractors = new Dictionary<KeywordInRepository.KeywordInRepositoryType, BasicExtractor>();
            _keywordExtractors.Add(KeywordInRepository.KeywordInRepositoryType.InReadmeRake1,
                new RakeExtractor(1));
            _keywordExtractors.Add(KeywordInRepository.KeywordInRepositoryType.InReadmeRake2,
                new RakeExtractor(2));
            _keywordExtractors.Add(KeywordInRepository.KeywordInRepositoryType.InReadmeRake3,
                new RakeExtractor(3));
            _keywordExtractors.Add(KeywordInRepository.KeywordInRepositoryType.InReadmeRake4,
                new RakeExtractor(4));
            _keywordExtractors.Add(KeywordInRepository.KeywordInRepositoryType.InReadmeTextRank,
                new TextRankExtractor());
            _keywordExtractors.Add(KeywordInRepository.KeywordInRepositoryType.InReadmeEdNormal,
                new EntropyDifferenceExtractor());
            _keywordExtractors.Add(KeywordInRepository.KeywordInRepositoryType.InReadmeEdMax,
                new EntropyDifferenceExtractor(true));

            _databaseConnection =
                new DatabaseConnection(mySqlUsername, mySqlPassword, mySqlDatabase, mySqlHost, mySqlPort);
        }

        private static void WaitUntil(DateTimeOffset time)
        {
            TimeSpan waittime = time - DateTimeOffset.Now;
            Write(" --- Rate limit reached! Waiting {0} seconds --- ", waittime.TotalSeconds);
            if (waittime > new TimeSpan(0))
                Thread.Sleep(waittime);
        }

        private async Task WaitForApiLimit(bool search = false)
        {
            //check if local managed limit exceeded!
            if (search && _searchLimit == 0 || !search && _rateLimit == 0)
            {
                //update local values in case of limit reset after time
                MiscellaneousRateLimit limits = await _gitHubClient.Miscellaneous.GetRateLimits();
                //if a limit is really exceeded!
                while (search && limits.Resources.Search.Remaining == 0)
                {
                    WaitUntil(limits.Resources.Search.Reset);
                    limits = await _gitHubClient.Miscellaneous.GetRateLimits();
                }

                while (!search && limits.Resources.Core.Remaining == 0)
                {
                    WaitUntil(limits.Resources.Core.Reset);
                    limits = await _gitHubClient.Miscellaneous.GetRateLimits();
                }

                // store the new limits
                _searchLimit = limits.Resources.Search.Remaining;
                _rateLimit = limits.Resources.Core.Remaining;
            }

            // decrease the limit for the action
            if (search)
                _searchLimit--;
            else
                _rateLimit--;
        }

        private async Task<string> GetReadmeMd(long repositoryId, bool afterException = false)
        {
            await WaitForApiLimit();
            try
            {
                return (await _repositoryContentsClient.GetReadme(repositoryId)).Content;
            }
            catch (NotFoundException)
            {
                return "";
            }
            catch (ApiException e)
            {
                Write("--- ApiException: {0} waiting 60 seconds --- ", e.Message);
                Thread.Sleep(60 * 1000);
                if (!afterException)
                    return await GetReadmeMd(repositoryId, true);

                throw new Exception("Second exception, within GetReadme");
            }
        }

        private async Task<string[]> GetFiles(long repositoryId, string extension, bool afterException = false)
        {
            List<string> files = new List<string>();
            await WaitForApiLimit();
            TreeResponse tree;
            try
            {
                tree = await _gitHubClient.Git.Tree.GetRecursive(repositoryId, "HEAD");
            }
            catch (ApiException e)
            {
                Write("--- ApiException: {0} waiting 60 seconds --- ", e.Message);
                Thread.Sleep(60 * 1000);
                if (!afterException)
                    return await GetFiles(repositoryId, extension, true);

                throw new Exception("Second exception, within GetFiles");
            }

            foreach (TreeItem treeItem in tree.Tree)
                if (treeItem.Path.EndsWith(extension, StringComparison.Ordinal))
                    files.Add(Path.GetFileNameWithoutExtension(treeItem.Path)?.ToLower().Trim());

            return files.Distinct().ToArray();
        }

        private async Task<SearchCodeResult> SearchCode(SearchCodeRequest request, bool afterException = false)
        {
            await WaitForApiLimit(true);
            try
            {
                return await _gitHubClient.Search.SearchCode(request);
            }
            catch (AbuseException e)
            {
                Write("--- AbuseException! Waiting {0} seconds --- ", e.RetryAfterSeconds);
                Thread.Sleep(1000 * (e.RetryAfterSeconds ?? 60));
                if (!afterException)
                    return await SearchCode(request, true);

                throw new Exception("Second exception, within SearchCode");
            }
            catch (ApiException e)
            {
                Write("--- ApiException: {0} waiting 60 seconds --- ", e.Message);
                Thread.Sleep(60 * 1000);
                if (!afterException)
                    return await SearchCode(request, true);

                throw new Exception("Second exception, within SearchCode");
            }
        }

        private async Task<Repository> GetRepository(long repositoryId, bool afterException = false)
        {
            await WaitForApiLimit();
            try
            {
                return await _gitHubClient.Repository.Get(repositoryId);
            }
            catch (ApiException e)
            {
                Write("--- ApiException: {0} waiting 60 seconds --- ", e.Message);
                Thread.Sleep(60 * 1000);
                if (!afterException)
                    return await GetRepository(repositoryId, true);

                throw new Exception("Second exception, within GetRepository");
            }
        }

        /// <summary>
        ///     Create an XRealiSE_DBConnection.data.GitHubRepository from an Octokit.Repository.
        /// </summary>
        /// <param name="repo">A Octokit.Repository with informations to store.</param>
        /// <returns>A GitHubRepository with all storable informations from the given Repository</returns>
        private static GitHubRepository FromCrawled(Repository repo)
        {
            return new GitHubRepository
            {
                CreatedAt = repo.CreatedAt.UtcDateTime,
                Description = repo.Description?.Length > 255
                    ? repo.Description.Substring(0, 255)
                    : repo.Description,
                ForksCount = repo.ForksCount,
                GitHubRepositoryId = repo.Id,
                HasDownloads = repo.HasDownloads,
                HasIssues = repo.HasIssues,
                HasPages = repo.HasPages,
                HasWiki = repo.HasWiki,
                License = repo.License?.Name,
                Name = repo.Name,
                OpenIssuesCount = repo.OpenIssuesCount,
                Owner = repo.Owner.Login,
                PushedAt = repo.PushedAt?.UtcDateTime,
                StargazersCount = repo.StargazersCount,
                UpdatedAt = repo.UpdatedAt.UtcDateTime,
                WatchersCount = repo.WatchersCount
            };
        }

        private static string StripHtml(string input)
        {
            return Regex.Replace(input, @"</?[a-zA-Z]+[^>]*>", string.Empty);
        }

        private static string StripUrls(string input)
        {
            return Regex.Replace(input, @"https?\:\/\/[^\s]*", string.Empty);
        }

        private static string StripNonAlphanumeric(string input)
        {
            return Regex.Replace(input, @"[^\s\w\d'-]", string.Empty);
        }

        public async Task Crawl(int from, int to, string extra = "")
        {
            _crawledRepos = new List<long>();
            await CrawlRecursive(from, to, extra);
            //delete all unvisited & ( unreachable | without unity.com.xr )

            WriteLine("Main crawling done, crawled {0} repos, cleanup and check of unfound and already crawled repos.",
                _crawledRepos.Count);

            GitHubRepository[] oldRepos = _databaseConnection.GitHubRepositories
                .Where(r => !_crawledRepos.Contains(r.GitHubRepositoryId)).ToArray();
            //Search code specific for each unfound repo in case they were not found because of uncached searches
            WriteLine("{0} repositories in DB that werent found by crawler.", oldRepos.Length);
            SearchCodeRequest request = new SearchCodeRequest("com.unity.xr" + extra)
            {
                FileName = "manifest.json",
                PerPage = 100,
                Page = 1
            };
            foreach (GitHubRepository oldGitHubRepository in oldRepos)
            {
                Write("Repository {0}/{1} ", oldGitHubRepository.Owner, oldGitHubRepository.Name);
                request.Repos.Clear();
                request.Repos.Add(oldGitHubRepository.Owner, oldGitHubRepository.Name);

                SearchCodeResult result = await SearchCode(request);
                if (result.TotalCount <= 0)
                {
                    WriteLine("[ToDelete]");
                    continue;
                }

                _crawledRepos.Add(oldGitHubRepository.GitHubRepositoryId);
                await CrawlRepository(oldGitHubRepository.GitHubRepositoryId);
            }

            await _databaseConnection.SaveChangesAsync();

            // delete all repos that are not matching the criteria of the crawler
            _databaseConnection.GitHubRepositories.RemoveRange(oldRepos.Where(r =>
                !_crawledRepos.Contains(r.GitHubRepositoryId)));
            await _databaseConnection.SaveChangesAsync();

            // delete all keywords without connection
            _databaseConnection.Keywords.RemoveRange(
                _databaseConnection.Keywords.Where(k => k.KeywordInRepositories.Count == 0));
            await _databaseConnection.SaveChangesAsync();

            WriteLine("Database cleanup done.");
        }

        private async Task CrawlRepository(long id)
        {
            Repository repo = await GetRepository(id);

            GitHubRepository gitHubRepository = FromCrawled(repo);

            bool updatekeywords = _databaseConnection.InsertOrUpdateRepository(ref gitHubRepository);

            Write("[Repository]");

            if (updatekeywords)
            {
                //Remove all old keyword connections
                if (gitHubRepository.KeywordInRepositories != null)
                {
                    _databaseConnection.KeywordInRepositories.RemoveRange(gitHubRepository
                        .KeywordInRepositories);
                    await _databaseConnection.SaveChangesAsync();
                }

                string readme = await GetReadmeMd(repo.Id);
                string strippedreadme = StripNonAlphanumeric(StripUrls(StripHtml(readme)));

                if (strippedreadme.Length > 0)
                    foreach ((KeywordInRepository.KeywordInRepositoryType type, BasicExtractor extractor) in
                        _keywordExtractors)
                    {
                        Dictionary<string, double> keywords = extractor.GetKeywords(strippedreadme);
                        foreach ((string keyword, double weight) in keywords.Where(keyword =>
                            keyword.Key.Length <= 120))
                            _databaseConnection.AddKeywordConnection(gitHubRepository, keyword.Trim(), type, weight);
                        Write("[{0}]", type.ToString());
                    }
                else
                    Write("[NoReadme]");

                string[] files = await GetFiles(repo.Id, "cs");
                foreach (string filename in files.Where(filename => filename.Length <= 120))
                    _databaseConnection.AddKeywordConnection(gitHubRepository, filename,
                        KeywordInRepository.KeywordInRepositoryType.Classname);
                WriteLine("[ClassNames]");
            }
            else
            {
                WriteLine("[RepositoryUnchanged]");
            }
        }

        private async Task CrawlRecursive(int from, int to, string extra)
        {
            Write("Crawling sizes from {0} to {1}, ", from, to);

            SearchCodeRequest request = new SearchCodeRequest("com.unity.xr" + extra)
            {
                Size = new Range(from, to),
                FileName = "manifest.json",
                PerPage = 100,
                Page = 1
            };

            SearchCodeResult result = await SearchCode(request);

            Write("{0} results, ", result.TotalCount);
            if (result.TotalCount > 1000)
            {
                if (from != to)
                {
                    int middle = (from + to) / 2;
                    WriteLine("split at {0}", middle);
                    await CrawlRecursive(from, middle, extra);
                    await CrawlRecursive(middle + 1, to, extra);
                }
                else
                {
                    WriteLine("split with a fragile hack");
                    await CrawlRecursive(from, to, " path:packages");
                    await CrawlRecursive(from, to, " NOT path:packages");
                }
            }
            else
            {
                WriteLine("NO split");
                do
                {
                    WriteLine("Page {0}", request.Page);
                    foreach (SearchCode file in result.Items)
                    {
                        Write("Repository {0} ", file.Repository.FullName);
                        if (!_crawledRepos.Contains(file.Repository.Id))
                        {
                            _crawledRepos.Add(file.Repository.Id);
                            await CrawlRepository(file.Repository.Id);
                        }
                        else
                        {
                            WriteLine("[AlreadyCrawled]");
                        }
                    }

                    await _databaseConnection.SaveChangesAsync();
                    WriteLine("Database Changes Saved.");

                    if (++request.Page <= 10)
                        result = await SearchCode(request);
                } while (result.Items.Count > 0 && request.Page <= 10);
            }
        }
    }
}
