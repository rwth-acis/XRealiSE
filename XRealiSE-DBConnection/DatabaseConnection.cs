using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MySql.Data;
using MySql.Data.MySqlClient;
using XRealiSE_DBConnection.data;

namespace XRealiSE_DBConnection
{
    public sealed class DatabaseConnection : DbContext
    {
        /// <inheritdoc />
        /// <summary>
        /// </summary>
        /// <param name="databaseUser"></param>
        /// <param name="databasePassword"></param>
        /// <param name="databaseName"></param>
        /// <param name="databaseHost"></param>
        /// <param name="databasePort"></param>
        public DatabaseConnection(string databaseUser = "root", string databasePassword = "",
            string databaseName = "xrealise", string databaseHost = "localhost", uint databasePort = 3306)
        {
            MySqlConnectionStringBuilder connectionStringBuilder = new MySqlConnectionStringBuilder
            {
                UserID = databaseUser,
                Password = databasePassword,
                Database = databaseName,
                Server = databaseHost,
                Port = databasePort
            };
            databaseConnectionString = connectionStringBuilder.ConnectionString;
            generatedKeywords = new Dictionary<string, Keyword>();
            Database.EnsureCreated();

            foreach (Keyword keyword in Keywords)
            {
                generatedKeywords.Add(keyword.Word, keyword);
            }
        }

        protected Dictionary<string, Keyword> generatedKeywords;
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseMySQL(databaseConnectionString);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<GitHubRepository>().Property(e => e.CreatedAt)
                .HasDefaultValueSql("'0000-00-00 00:00:00'");
            modelBuilder.Entity<Keyword>();
            modelBuilder.Entity<KeywordInRepository>().HasKey(e => new {e.KeywordId, e.GitHubRepositoryId, e.Type});
        }

        public DbSet<GitHubRepository> GitHubRepositories { get; set; }
        public DbSet<KeywordInRepository> KeywordInRepositories { get; set; }
        public DbSet<Keyword> Keywords { get; set; }

        public void insertOrUpdateRepository(ref GitHubRepository repository)
        {
            GitHubRepository existing = GitHubRepositories.Find(repository.GitHubRepositoryId);
            if (existing == null)
                GitHubRepositories.Add(repository);
            else
            {
                existing.Update(repository);
                //SaveChanges();
                repository = existing;
            }
        }

        public void addKeywordConnection(GitHubRepository repository, string keyword, KeywordInRepository.KeywordInRepositoryType type, double weight = 1)
        {
            Keyword existingKeyword = null;
            if (generatedKeywords.ContainsKey(keyword))
                existingKeyword = generatedKeywords[keyword];
            else
            {
                //Add keyword

                existingKeyword = new Keyword {Word = keyword};
                Keywords.Add(existingKeyword);
                generatedKeywords.Add(keyword, existingKeyword);


            }


            KeywordInRepository existingConnection = KeywordInRepositories.Find(existingKeyword.KeywordId, repository.GitHubRepositoryId, type);
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

            //SaveChanges();


        }



        private readonly string databaseConnectionString;
    }
}