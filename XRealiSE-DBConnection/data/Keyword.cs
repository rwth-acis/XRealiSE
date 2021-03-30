#region

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using MySql.EntityFrameworkCore.DataAnnotations;

#endregion

namespace XRealiSE_DBConnection.data
{
    [MySqlCharset("utf8")]
    [Index(nameof(Word), IsUnique = true)]
    public class Keyword
    {
        [Key] public long KeywordId { get; set; }

        [Required]
        [Column(TypeName = "varchar(120)")]
        [MySqlCharset("utf8_bin")]
        public string Word { get; set; }

        public virtual ICollection<KeywordInRepository> KeywordInRepositories { get; set; }
    }
}
