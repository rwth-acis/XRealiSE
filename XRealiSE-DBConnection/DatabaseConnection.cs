#region

using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using XRealiSE_DBConnection.data;

#endregion

namespace XRealiSE_DBConnection
{
    public sealed class DatabaseConnection : DbContext
    {
        private readonly string _databaseConnectionString;
        private Dictionary<string, Keyword> _generatedKeywords;

        /// <summary>
        /// Initializes a new DatabaseConnection to the MySQL database specified within the connection
        /// parameters. This also ensures that a database will be created if none is present.
        /// </summary>
        /// <param name="databaseUser">The username for the MySQL database</param>
        /// <param name="databasePassword">The password for the MySQL database</param>
        /// <param name="databaseName">The name of the database where all data will be stored</param>
        /// <param name="databaseHost">The host for the MySQL server</param>
        /// <param name="databasePort">The connection port for the MySQL server</param>
        public DatabaseConnection(string databaseUser = "root", string databasePassword = "",
            string databaseName = "xrealise", string databaseHost = "localhost", uint databasePort = 3306)
        {
            _databaseConnectionString = new MySqlConnectionStringBuilder
            {
                UserID = databaseUser,
                Password = databasePassword,
                Database = databaseName,
                Server = databaseHost,
                Port = databasePort,
                CharacterSet = "utf8mb4"
            }.ConnectionString;
            _generatedKeywords = new Dictionary<string, Keyword>();

            Database.EnsureCreated();
            
            
            // Generating a local database of keywords because a search for an existing keyword in the DbSet
            // is extremely slow.
            foreach (Keyword keyword in Keywords) _generatedKeywords.Add(keyword.Word, keyword);
        }

        public DbSet<GitHubRepository> GitHubRepositories { get; set; }
        public DbSet<KeywordInRepository> KeywordInRepositories { get; set; }
        public DbSet<Keyword> Keywords { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseMySQL(_databaseConnectionString);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.UseCollation("utf8_general_ci");
            modelBuilder.Entity<GitHubRepository>().Property(e => e.CreatedAt)
                .HasDefaultValueSql("'0000-00-00 00:00:00'");
            modelBuilder.Entity<Keyword>();
            // Three column primary key
            modelBuilder.Entity<KeywordInRepository>().HasKey(e => new {e.KeywordId, e.GitHubRepositoryId, e.Type});
        }

        /// <summary>
        /// Add a new repository the crawled data. If that repository already exists (check will be done on the
        /// GitHubRepositoryId) the repository data will be updated.
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

        /// <summary>
        /// Adds a new keyword connection for a repository to the database. If that connection already exists the
        /// weight will be updated.
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
