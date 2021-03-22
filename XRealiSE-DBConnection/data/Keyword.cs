using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace XRealiSE_DBConnection.data
{
    [Index(nameof(Word), IsUnique = true)]
    public class Keyword
    {
        [Key]
        public long KeywordId { get; set; }
        [Required]
        [Column(TypeName = "varchar(100)")]
        public string Word { get; set; }
        public virtual ICollection<KeywordInRepository> KeywordInRepositories { get; set; }
    }
}
