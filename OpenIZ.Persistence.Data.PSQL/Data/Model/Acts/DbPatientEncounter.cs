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


using PetaPoco;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenIZ.Persistence.Data.PSQL.Data.Model.Acts
{
    /// <summary>
    /// Represents storage class for a patient encounter
    /// </summary>
    [TableName("pat_enc_tbl")]
    public class DbPatientEncounter : DbActSubTable
    {

        /// <summary>
        /// Identifies the manner in which the patient was discharged
        /// </summary>
        [Column("dsch_dsp_cd_id")]
        public Guid DischargeDispositionKey { get; set; }
    }
}