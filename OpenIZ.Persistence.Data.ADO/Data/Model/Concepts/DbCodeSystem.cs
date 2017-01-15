﻿using OpenIZ.Persistence.Data.ADO.Data.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenIZ.Persistence.Data.ADO.Data.Model.Concepts
{
    /// <summary>
    /// Represents a code system 
    /// </summary>
    [Table("cd_sys_tbl")]
    public class DbCodeSystem : DbNonVersionedBaseData
    {
        /// <summary>
        /// Gets or sets the code system id
        /// </summary>
        [Column("cs_id")]
        public override Guid Key { get; set; }

        /// <summary>
        /// Gets or sets the name of the code system
        /// </summary>
        [Column("cs_name")]
        public String Name { get; set; }

        /// <summary>
        /// Gets or sets the oid
        /// </summary>
        [Column("oid")]
        public String Oid { get; set; }

        /// <summary>
        /// Gets or sets the domain CX.4
        /// </summary>
        [Column("domain")]
        public String Domain { get; set; }

        /// <summary>
        /// Gets or sets the url
        /// </summary>
        [Column("url")]
        public String Url { get; set; }

        /// <summary>
        /// Gets or sets the version text from the CS authorty
        /// </summary>
        [Column("vrsn_txt")]
        public String VersionText { get; set; }

        /// <summary>
        /// Gets or sets the description
        /// </summary>
        [Column("descr")]
        public String Description { get; set; }
    }
}