﻿using OpenIZ.Persistence.Data.ADO.Data.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenIZ.Persistence.Data.ADO.Data.Model.Concepts
{
    /// <summary>
    /// Reference term name
    /// </summary>
    [Table("ref_term_name_tbl")]
    public class DbReferenceTermName : DbBaseData, IDbAssociation
    {
        /// <summary>
        /// Gets or sets the key
        /// </summary>
        [Column("ref_term_name_id")]
        public override Guid Key { get; set; }

        /// <summary>
        /// Gets or sets the ref term to which the nae applies
        /// </summary>
        [Column("ref_term_id")]
        public Guid SourceKey { get; set; }

        /// <summary>
        /// Gets or sets the language code
        /// </summary>
        [Column("lang_cs")]
        public String LanguageCode { get; set; }

        /// <summary>
        /// Gets orsets the value
        /// </summary>
        [Column("term_name")]
        public String Value { get; set; }

        /// <summary>
        /// Gets or sets the phonetic code
        /// </summary>
        [Column("phon_cs")]
        public String PhoneticCode { get; set; }

        /// <summary>
        /// Gets or sets the algorithm id
        /// </summary>
        [Column("phon_alg_id")]
        public Guid PhoneticAlgorithm { get; set; }
    }
}