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
 * Date: 2016-6-28
 */


using PetaPoco;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenIZ.Persistence.Data.PSQL.Data.Model.Entities
{
    /// <summary>
    /// Represents the orm class for place service
    /// </summary>
    [TableName("plc_svc_tbl")]
    public class DbPlaceService : DbEntityVersionedAssociation
    {

        /// <summary>
        /// Gets or sets the service schedule information
        /// </summary>
        [Column("schdl")]
        public Guid ServiceSchedule { get; set; }

        /// <summary>
        /// Gets or sets the service concept
        /// </summary>
        [Column("svc_cd_id")]
        public Guid ServiceConceptKey { get; set; }

        /// <summary>
        /// Primary key
        /// </summary>
        [Column("svc_id")]
        public override Guid Key { get; set; }
    }
}