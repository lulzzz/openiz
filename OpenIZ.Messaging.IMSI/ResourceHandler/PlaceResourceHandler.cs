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
 * Date: 2016-7-18
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenIZ.Core.Model;
using OpenIZ.Core.Model.Query;
using MARC.HI.EHRS.SVC.Core;
using OpenIZ.Core.Services;
using OpenIZ.Core.Model.Entities;
using OpenIZ.Core.Model.Collection;
using OpenIZ.Core.Security;
using System.Security.Permissions;
using OpenIZ.Core.Security.Attribute;

namespace OpenIZ.Messaging.IMSI.ResourceHandler
{
    /// <summary>
    /// Represents a resource handler which queries places
    /// </summary>
    public class PlaceResourceHandler : IResourceHandler
    {
        // Repository
        private IPlaceRepositoryService m_repository;

        /// <summary>
        /// Place resource handler subscription
        /// </summary>
        public PlaceResourceHandler()
        {
            ApplicationContext.Current.Started += (o, e) => this.m_repository = ApplicationContext.Current.GetService<IPlaceRepositoryService>();
        }

        /// <summary>
        /// Gets the resource name
        /// </summary>
        public string ResourceName
        {
            get
            {
                return "Place";
            }
        }

        /// <summary>
        /// Gets the type this constructs
        /// </summary>
        public Type Type
        {
            get
            {
                return typeof(Place);
            }
        }

        /// <summary>
        /// Creates the specified place 
        /// </summary>
        [PolicyPermission(SecurityAction.Demand, PolicyId = PermissionPolicyIdentifiers.UnrestrictedAdministration)]
        public IdentifiedData Create(IdentifiedData data, bool updateIfExists)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            Bundle bundleData = data as Bundle;
            bundleData?.Reconstitute();
            var processData = bundleData?.Entry ?? data;

            if (processData is Bundle) // Client submitted a bundle
                throw new InvalidOperationException("Bundle must have an entry point");
            else if (processData is Place)
            {
                var placeData = processData as Place;
                if (updateIfExists)
                    return this.m_repository.Save(placeData);
                else
                    return this.m_repository.Insert(placeData);
            }
            else
                throw new ArgumentException(nameof(data), "Invalid data type");
        }

        /// <summary>
        /// Gets the specified data
        /// </summary>
        public IdentifiedData Get(Guid id, Guid versionId)
        {
            return this.m_repository.Get(id, versionId);
        }

        /// <summary>
        /// Obsoletes the specified data
        /// </summary>
        [PolicyPermission(SecurityAction.Demand, PolicyId = PermissionPolicyIdentifiers.UnrestrictedAdministration)]
        public IdentifiedData Obsolete(Guid key)
        {
            return this.m_repository.Obsolete(key);
        }

        /// <summary>
        /// Queries for the specified data
        /// </summary>
        public IEnumerable<IdentifiedData> Query(NameValueCollection queryParameters)
        {
            return this.m_repository.Find(QueryExpressionParser.BuildLinqExpression<Place>(queryParameters));
        }

        /// <summary>
        /// Query for specified data with limits
        /// </summary>
        public IEnumerable<IdentifiedData> Query(NameValueCollection queryParameters, int offset, int count, out int totalCount)
        {
            return this.m_repository.Find(QueryExpressionParser.BuildLinqExpression<Place>(queryParameters), offset, count, out totalCount);
        }

        /// <summary>
        /// Updates the specified object
        /// </summary>
        public IdentifiedData Update(IdentifiedData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            var bundleData = data as Bundle;
            bundleData?.Reconstitute();
            var saveData = bundleData?.Entry ?? data;

            if (saveData is Bundle)
                throw new InvalidOperationException("Bundle must have an entry");
            else if (saveData is Place)
                return this.m_repository.Save(saveData as Place);
            else
                throw new ArgumentException(nameof(data), "Invalid storage type");
        }
    }
}