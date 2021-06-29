#region

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MySql.EntityFrameworkCore.DataAnnotations;

#endregion

namespace XRealiSE_DBConnection.data
{
    [MySqlCharset("utf8")]
    public class Search
    {
        [Key] public int SearchId { get; set; }

        [Required]
        [Column(TypeName = "varchar(255)")]
        [MySqlCharset("utf8")]
        public string SearchString { get; set; }

        [Required]
        [Column(TypeName = "varchar(255)")]
        [MySqlCharset("utf8")]
        public string SearchOrdering { get; set; }

        [Required]
        [Column(TypeName = "varchar(255)")]
        [MySqlCharset("utf8")]
        public string SearchFilters { get; set; }

        [Required] public DateTime SearchTime { get; set; }
        [Required] public int SearchResultSize { get; set; }

        [Required] public long SearchDuration { get; set; }
        [Required] public string SelectedVersions { get; set; }

        [ForeignKey("ParentSearch")] public int? ParentSearchId { get; set; }

        public virtual Search ParentSearch { get; set; }
    }
}
