#region

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MySql.EntityFrameworkCore.DataAnnotations;

#endregion

namespace XRealiSE_DBConnection.data
{
    [MySqlCharset("utf8")]
    public class SearchAction
    {
        [Key] public int SearchActionId { get; set; }

        [Required] [ForeignKey("Search")] public int SearchId { get; set; }

        [Required] public virtual Search Search { get; set; }

        [Required] public DateTime ActionTime { get; set; }

        [Required]
        [Column(TypeName = "varchar(255)")]
        [MySqlCharset("utf8")]
        public string ActionDescription { get; set; }
    }
}
