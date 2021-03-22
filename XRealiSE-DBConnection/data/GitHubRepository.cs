using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace XRealiSE_DBConnection.data
{
    [Index(nameof(Name), nameof(Owner), IsUnique = true)]
    public class GitHubRepository
    {
        [Required]
        public DateTimeOffset CreatedAt { get; set; }
        [Column(TypeName = "varchar(255)")]
        public string Description { get; set; }
        [Required]
        public int ForksCount { get; set; }
        [Required]
        public bool HasDownloads { get; set; }
        [Required]
        public bool HasIssues { get; set; }
        [Required]
        public bool HasPages { get; set; }
        [Required]
        public bool HasWiki { get; set; }
        [Key]
        public long GitHubRepositoryId { get; set; }
        [Column(TypeName = "varchar(150)")]
        public string License { get; set; }
        [Required]
        [Column(TypeName = "varchar(100)")]
        public string Name { get; set; }
        [Required]
        public int OpenIssuesCount { get; set; }
        [Required]
        [Column(TypeName = "varchar(100)")]
        public string Owner { get; set; }
        public DateTimeOffset? PushedAt { get; set; }
        [Required]
        public int StargazersCount { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        [Required]
        public int WatchersCount { get; set; }
        public virtual ICollection<KeywordInRepository> KeywordInRepositories { get; set; }

        public void Update(GitHubRepository newValues)
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
