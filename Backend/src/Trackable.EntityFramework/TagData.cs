﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Trackable.EntityFramework
{
    [Table("Tags")]
    public class TagData : EntityBase<int>
    {
        [Column(TypeName = "VARCHAR")]
        [StringLength(250)]
        public string TagName { get; set; }
    }
}