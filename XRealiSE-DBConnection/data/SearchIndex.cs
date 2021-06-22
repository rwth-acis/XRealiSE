#region

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using MySql.EntityFrameworkCore.DataAnnotations;

#endregion

namespace XRealiSE_DBConnection.data
{
    [MySqlCharset("utf8")]
    public class SearchIndex
    {
        [Required]
        [Column(TypeName = "MEDIUMTEXT")]
        [MySqlCollation("utf8_bin")]
        [MySqlCharset("utf8")]
        public string SearchString { get; set; }

        [Key]
        [Required]
        [ForeignKey("GitHubRepository")]
        public long GitHubRepositoryId { get; set; }

        public virtual GitHubRepository GitHubRepository { get; set; }
    }
}
