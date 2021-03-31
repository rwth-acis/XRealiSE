using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Octokit;
using Octokit.Internal;
using XRealiSE_DBConnection;
using XRealiSE_DBConnection.data;
using static System.Console;
using RakeExtraction = Rake.Rake;
using Range = Octokit.Range;

namespace XRealiSE_Crawler
{
    class Crawler
    {
        private RakeExtraction _rake;
        private List<long> _crawledRepos;
        private GitHubClient _gitHubClient;
        private RepositoryContentsClient _repositoryContentsClient;
        private int _searchLimit;
        private int _rateLimit;
        private DatabaseConnection _databaseConnection;

        public Crawler(string apikey)
        {
            ProductHeaderValue productHeaderValue = new ProductHeaderValue("xrealise");
          
            _gitHubClient = new GitHubClient(productHeaderValue, new InMemoryCredentialStore(new Credentials(apikey)));
            _repositoryContentsClient =
                new RepositoryContentsClient(
                    new ApiConnection(new Connection(productHeaderValue, new InMemoryCredentialStore(new Credentials(apikey)))));
            _crawledRepos = new List<long>();
            _rake = new RakeExtraction(LoadStopWords("SmartStoplist.txt"), 2, 4);
            
            _databaseConnection = new DatabaseConnection();
            
        }

        private static HashSet<string> LoadStopWords(string path)
        {
            HashSet<string> words = new HashSet<string>();
            using StreamReader f = File.OpenText(path);
            while (!f.EndOfStream)
            {
                string line = f.ReadLine();
                if (line?[0] != '#')
                    words.Add(line);
            }

            return words;
        }

        private static void WaitUntil(DateTimeOffset time)
        {
            TimeSpan waittime = time - DateTimeOffset.Now;
            WriteLine("RATE LIMIT REACHED! Waiting {0} seconds.", waittime.TotalSeconds);
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

        private async Task<string> GetReadmeMd(long repositoryId)
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
        }

        private async Task<string[]> GetFiles(long repositoryId, string extension)
        {
            List<string> files = new List<string>();
            await WaitForApiLimit();
            TreeResponse tree = await _gitHubClient.Git.Tree.GetRecursive(repositoryId, "HEAD");
            foreach (TreeItem treeItem in tree.Tree)
                if (treeItem.Path.EndsWith(extension, StringComparison.Ordinal))
                    files.Add(Path.GetFileNameWithoutExtension(treeItem.Path)?.ToLower().Trim());

            return files.Distinct().ToArray();
        }


        private async Task<SearchCodeResult> SearchCode(SearchCodeRequest request)
        {
            await WaitForApiLimit(true);
            return await _gitHubClient.Search.SearchCode(request);
        }

        private async Task<Repository> GetRepository(long repositoryId)
        {
            await WaitForApiLimit();
            return await _gitHubClient.Repository.Get(repositoryId);
        }

        /// <summary>
        /// Create an XRealiSE_DBConnection.data.GitHubRepository from an Octokit.Repository.
        /// </summary>
        /// <param name="repo">A Octokit.Repository with informations to store.</param>
        /// <returns>A GitHubRepository with all storable informations from the given Repository</returns>
        private static GitHubRepository FromCrawled(Repository repo)
        {
            return new GitHubRepository
            {
                CreatedAt = repo.CreatedAt,
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
                PushedAt = repo.PushedAt,
                StargazersCount = repo.StargazersCount,
                UpdatedAt = repo.UpdatedAt,
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
                    await Crawl(from, middle);
                    await Crawl(middle + 1, to);
                }
                else
                {
                    WriteLine("split with a fragile hack");
                    await Crawl(from, to, " path:packages");
                    await Crawl(from, to, " NOT path:packages");
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

                            Repository repo = await GetRepository(file.Repository.Id);

                            GitHubRepository gitHubRepository = FromCrawled(repo);

                            _databaseConnection.InsertOrUpdateRepository(ref gitHubRepository);

                            Write("[Repository]");

                            string readme = await GetReadmeMd(repo.Id);
                            string strippedreadme = StripNonAlphanumeric(StripUrls(StripHtml(readme)));

                            if (readme.Length > 0)
                            {
                                Dictionary<string, double> keywords = _rake.Run(strippedreadme);
                                foreach (KeyValuePair<string, double> keyword in keywords)
                                    if (keyword.Key.Length <= 120)
                                        _databaseConnection.AddKeywordConnection(gitHubRepository,
                                            keyword.Key.Trim(),
                                            KeywordInRepository.KeywordInRepositoryType.InReadme, keyword.Value);
                            }

                            Write("[Readme]");

                            string[] files = await GetFiles(repo.Id, "cs");
                            foreach (string filename in files.Where(filename => filename.Length <= 120))
                                _databaseConnection.AddKeywordConnection(gitHubRepository, filename,
                                    KeywordInRepository.KeywordInRepositoryType.Classname);

                            Write("[ClassNames]");

                            await _databaseConnection.SaveChangesAsync();
                            WriteLine("[Saved]");
                        }
                        else
                        {
                            WriteLine("Already Crawled this run!");
                        }
                    }

                    if (++request.Page <= 10)
                        result = await SearchCode(request);
                } while (result.Items.Count > 0 && request.Page <= 10);
            }
        }
    }
}
