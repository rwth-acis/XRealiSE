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
        private readonly Dictionary<string, Keyword> _generatedKeywords;

        public DatabaseConnection(string databaseUser = "root", string databasePassword = "",
            string databaseName = "xrealise", string databaseHost = "localhost", uint databasePort = 3306)
        {
            MySqlConnectionStringBuilder connectionStringBuilder = new MySqlConnectionStringBuilder
            {
                UserID = databaseUser,
                Password = databasePassword,
                Database = databaseName,
                Server = databaseHost,
                Port = databasePort,
                CharacterSet = "utf8mb4"
            };
            _databaseConnectionString = connectionStringBuilder.ConnectionString;
            _generatedKeywords = new Dictionary<string, Keyword>();
            Database.EnsureCreated();

            foreach (Keyword keyword in Keywords) _generatedKeywords.Add(keyword.Word, keyword);
        }

        public DbSet<GitHubRepository> GitHubRepositories { get; }
        public DbSet<KeywordInRepository> KeywordInRepositories { get; }
        public DbSet<Keyword> Keywords { get; }

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
            modelBuilder.Entity<KeywordInRepository>().HasKey(e => new {e.KeywordId, e.GitHubRepositoryId, e.Type});
        }

        public void InsertOrUpdateRepository(ref GitHubRepository repository)
        {
            GitHubRepository existing = GitHubRepositories.Find(repository.GitHubRepositoryId);
            if (existing == null)
            {
                GitHubRepositories.Add(repository);
            }
            else
            {
                existing.Update(repository);
                repository = existing;
            }
        }

        public void AddKeywordConnection(GitHubRepository repository, string keyword,
            KeywordInRepository.KeywordInRepositoryType type, double weight = 1)
        {
            Keyword existingKeyword;
            if (_generatedKeywords.ContainsKey(keyword))
            {
                existingKeyword = _generatedKeywords[keyword];
            }
            else
            {
                existingKeyword = new Keyword {Word = keyword};
                Keywords.Add(existingKeyword);
                _generatedKeywords.Add(keyword, existingKeyword);
            }

            KeywordInRepository existingConnection =
                KeywordInRepositories.Find(existingKeyword.KeywordId, repository.GitHubRepositoryId, type);
            if (existingConnection == null)
            {
                existingConnection = new KeywordInRepository
                    {Keyword = existingKeyword, Repository = repository, Weight = weight, Type = type};
                KeywordInRepositories.Add(existingConnection);
            }
            else
            {
                existingConnection.Weight = weight;
            }
        }
    }
}
