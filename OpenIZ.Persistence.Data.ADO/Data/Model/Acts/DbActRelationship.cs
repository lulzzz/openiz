﻿/*
 * Copyright 2015-2016 Mohawk College of Applied Arts and Technology
 * 
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); you 
 * may not use this file except in compliance with the License. You may 
 * obtain a copy of the License at 
 * 
 * http://www.apache.org/licenses/LICENSE-2.0 
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the 
 * License for the specific language governing permissions and limitations under 
 * the License.
 * 
 * User: justi
 * Date: 2016-7-1
 */


using OpenIZ.Persistence.Data.ADO.Data.Attributes;
using OpenIZ.Persistence.Data.ADO.Data.Model.Concepts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenIZ.Persistence.Data.ADO.Data.Model.Acts
{
    /// <summary>
    /// Identifies relationships between acts
    /// </summary>
    [Table("act_relationship")]
    public class DbActRelationship : DbVersionedAssociation
    {

        /// <summary>
        /// Gets or sets the source act key
        /// </summary>
        [Column("src_act_id"), ForeignKey(typeof(DbAct), "act_id")]
        public override Guid SourceKey { get; set; }

        /// <summary>
        /// Gets or sets the target entity key
        /// </summary>
        [Column("trg_act_id"), ForeignKey(typeof(DbAct), "act_id")]
        public Guid TargetKey { get; set; }

        /// <summary>
        /// Gets or sets the link type concept
        /// </summary>
        [Column("relationshipType"), ForeignKey(typeof(DbConcept), "cd_id")]
        public Guid RelationshipTypeKey { get; set; }

        /// <summary>
        /// Gets or sets the relationship id
        /// </summary>
        [Column("rel_id"), PrimaryKey]
        public override Guid Key { get; set; }
    }
}