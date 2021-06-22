#region

using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using XRealiSE_DBConnection.data;
using MySql.Data.MySqlClient;

#endregion

namespace XRealiSE_DBConnection
{
    public sealed class DatabaseConnection : DbContext
    {
        public static string DatabaseConnectionString;
        private readonly Dictionary<string, Keyword> _generatedKeywords;

        /// <inheritdoc />
        /// <summary>
        ///     Initializes a new DatabaseConnection to the MySQL database specified within the connection
        ///     parameters. This also ensures that a database will be created if none is present.
        /// </summary>
        /// <param name="keyWordCache">
        ///     Cache all existing keywords locally, used by the crawer for faster cheks of existing
        ///     keywords, used for the crawler
        /// </param>
        public DatabaseConnection(bool keyWordCache = false)
        {
            if (DatabaseConnectionString == null)
                throw new InvalidOperationException("DatabaseConnectionString must be set before initialising objects");

            _generatedKeywords = new Dictionary<string, Keyword>();

            Database.EnsureCreated();

            // Check if a fulltext index is given, since entity framework has no support for it
            // we use the "classic" mysql approach
            if (!_fullTextChecked)
            {
                using MySqlConnection connection = new MySqlConnection(DatabaseConnectionString);
                connection.Open();

                MySqlDataReader reader = new MySqlCommand("SHOW INDEX FROM searchindex;", connection).ExecuteReader();

                while (reader.Read())
                    if (reader.GetString("Key_name") == "SearchIndex" && reader.GetString("Column_name") == "SearchString" && reader.GetString("Index_type") == "FULLTEXT")
                        _fullTextChecked = true;

                reader.Close();

                if (!_fullTextChecked)
                {
                    MySqlCommand commandCreateFulltext =
                        new MySqlCommand(
                            "ALTER TABLE searchindex ADD FULLTEXT INDEX SearchIndex (SearchString);",
                            connection);
                    commandCreateFulltext.ExecuteNonQuery();
                    _fullTextChecked = true;
                }

                connection.Close();
            }


            // Generating a local database of keywords because a search for an existing keyword in the DbSet
            // is extremely slow.
            if (!keyWordCache) return;

            foreach (Keyword keyword in Keywords) _generatedKeywords.Add(keyword.Word, keyword);
        }

        public DbSet<GitHubRepository> GitHubRepositories { get; set; }
        public DbSet<KeywordInRepository> KeywordInRepositories { get; set; }
        public DbSet<Keyword> Keywords { get; set; }
        public DbSet<SearchIndex> SearchIndex { get; set; }
        public DbSet<Search> Searches { get; set; }
        public DbSet<SearchAction> SearchActions { get; set; }
        private static bool _fullTextChecked;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseLazyLoadingProxies();
            optionsBuilder.UseMySQL(DatabaseConnectionString);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.UseCollation("utf8_general_ci");
            modelBuilder.Entity<GitHubRepository>().Property(e => e.CreatedAt)
                .HasDefaultValueSql("'0000-00-00 00:00:00'");
            modelBuilder.Entity<Keyword>();
            modelBuilder.Entity<SearchIndex>();
            modelBuilder.Entity<Search>();
            modelBuilder.Entity<SearchAction>();

            // Three column primary key
            modelBuilder.Entity<KeywordInRepository>().HasKey(e => new {e.KeywordId, e.GitHubRepositoryId, e.Type});
        }

        /// <summary>
        ///     Add a new repository the crawled data. If that repository already exists (check will be done on the
        ///     GitHubRepositoryId) the repository data will be updated.
        /// </summary>
        /// <param name="repository">The repository to update or insert</param>
        /// <returns>If the last commit date was changed and therefore keywords needs to be updated</returns>
        public bool InsertOrUpdateRepository(ref GitHubRepository repository)
        {
            // Find already existing entry
            GitHubRepository existing = GitHubRepositories.Find(repository.GitHubRepositoryId);
            if (existing == null) // Repository is new => insert
            {
                GitHubRepositories.Add(repository);
                return true;
            }

            // Repository exists => Replace values in DB with values from reporitory parameter
            bool lastCommitChanged = existing.PushedAt != repository.PushedAt;
            existing.Update(repository);
            repository = existing;
            return lastCommitChanged;
        }

        public void InsertOrUpdateIndex(long gitHubRepositoryId, string index)
        {
            SearchIndex existing = SearchIndex.Find(gitHubRepositoryId);
            if (existing == null)
            {
                SearchIndex.Add(new SearchIndex {GitHubRepositoryId = gitHubRepositoryId, SearchString = index});
                return;
            }

            existing.SearchString = index;
        }

        /// <summary>
        ///     Adds a new keyword connection for a repository to the database. If that connection already exists the
        ///     weight will be updated.
        /// </summary>
        /// <param name="repository">The repository where the keyword belongs to</param>
        /// <param name="keyword">The keyword string</param>
        /// <param name="type">The type of the keyword connection</param>
        /// <param name="weight">The weight of the connection</param>
        public void AddKeywordConnection(GitHubRepository repository, string keyword,
            KeywordInRepository.KeywordInRepositoryType type, double weight = 1)
        {
            Keyword existingKeyword;
            if (_generatedKeywords.ContainsKey(keyword)) // If that keyword exists in the DB
            {
                existingKeyword = _generatedKeywords[keyword];
            }
            else // Insert it to add the connection
            {
                existingKeyword = new Keyword {Word = keyword};
                Keywords.Add(existingKeyword);
                _generatedKeywords.Add(keyword, existingKeyword);
            }

            // Chack if that connection already exists
            KeywordInRepository existingConnection =
                KeywordInRepositories.Find(existingKeyword.KeywordId, repository.GitHubRepositoryId, type);
            if (existingConnection == null) // if not insert that new connection
            {
                existingConnection = new KeywordInRepository
                    {Keyword = existingKeyword, Repository = repository, Weight = weight, Type = type};
                KeywordInRepositories.Add(existingConnection);
            }
            else // otherwise update the weight
            {
                existingConnection.Weight = weight;
            }
        }
    }
}
