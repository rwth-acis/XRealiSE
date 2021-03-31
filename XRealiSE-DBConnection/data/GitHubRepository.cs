#region

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using MySql.EntityFrameworkCore.DataAnnotations;

#endregion

namespace XRealiSE_DBConnection.data
{
    [MySqlCharset("utf8mb4")]
    [Index(nameof(Name), nameof(Owner), IsUnique = true)]
    public class GitHubRepository
    {
        [Required] public DateTime CreatedAt { get; set; }

        [Column(TypeName = "varchar(255)")]
        [MySqlCharset("utf8mb4")]
        public string Description { get; set; }

        [Required] public int ForksCount { get; set; }

        [Required] public bool HasDownloads { get; set; }

        [Required] public bool HasIssues { get; set; }

        [Required] public bool HasPages { get; set; }

        [Required] public bool HasWiki { get; set; }

        [Key] public long GitHubRepositoryId { get; set; }

        [Column(TypeName = "varchar(150)")]
        [MySqlCharset("utf8")]
        public string License { get; set; }

        [Required]
        [Column(TypeName = "varchar(100)")]
        [MySqlCharset("utf8")]
        public string Name { get; set; }

        [Required] public int OpenIssuesCount { get; set; }

        [Required]
        [Column(TypeName = "varchar(100)")]
        [MySqlCharset("utf8")]
        public string Owner { get; set; }

        public DateTime? PushedAt { get; set; }

        [Required] public int StargazersCount { get; set; }

        public DateTime UpdatedAt { get; set; }

        [Required] public int WatchersCount { get; set; }

        public virtual ICollection<KeywordInRepository> KeywordInRepositories { get; set; }

        /// <summary>
        /// Updates the GitHubRepository object fields with the values given in the newValues parameter
        /// </summary>
        /// <param name="newValues">The repository with values to replace the local stored</param>
        internal void Update(GitHubRepository newValues)
        {
            CreatedAt = newValues.CreatedAt;
            Description = newValues.Description;
            ForksCount = newValues.ForksCount;
            HasDownloads = newValues.HasDownloads;
            HasIssues = newValues.HasIssues;
            HasPages = newValues.HasPages;
            HasWiki = newValues.HasWiki;
            License = newValues.License;
            Name = newValues.Name;
            OpenIssuesCount = newValues.OpenIssuesCount;
            Owner = newValues.Owner;
            PushedAt = newValues.PushedAt;
            StargazersCount = newValues.StargazersCount;
            UpdatedAt = newValues.UpdatedAt;
            WatchersCount = newValues.WatchersCount;
        }
    }
}
