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
 * Date: 2016-6-14
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Schema;
using OpenIZ.Core.Model;
using System.Reflection;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Diagnostics;
using System.ServiceModel.Web;
using System.IO;
using OpenIZ.Core.Model.Attributes;
using System.Xml.Serialization;
using OpenIZ.Core.Model.DataTypes;
using OpenIZ.Core.Model.Constants;
using OpenIZ.Core.Model.Collection;
using OpenIZ.Core.Model.Acts;
using OpenIZ.Core.Model.Roles;
using OpenIZ.Core.Model.Entities;
using MARC.HI.EHRS.SVC.Core.Services;
using OpenIZ.Messaging.IMSI.ResourceHandler;
using OpenIZ.Messaging.IMSI.Model;
using System.Net;
using System.Data;
using System.Collections;
using OpenIZ.Core.Security.Attribute;
using System.Security.Permissions;
using OpenIZ.Core.Security;
using OpenIZ.Messaging.IMSI.Util;
using OpenIZ.Core.Model.Interfaces;
using MARC.Everest.Threading;
using System.Collections.Specialized;

namespace OpenIZ.Messaging.IMSI.Wcf
{
    /// <summary>
    /// Data implementation
    /// </summary>
    [ServiceBehavior(ConfigurationName = "IMSI")]
    public class ImsiServiceBehavior : IImsiServiceContract
    {
        // Trace source
        private TraceSource m_traceSource = new TraceSource("OpenIZ.Messaging.IMSI");

        // Lock object
        private object m_lockObject = new object();

        /// <summary>
        /// Load cache
        /// </summary>
        private Dictionary<Object, Object> m_loadCache = new Dictionary<Object, Object>();

        /// <summary>
        /// Create the specified resource
        /// </summary>
        [PolicyPermission(SecurityAction.Demand, PolicyId = PermissionPolicyIdentifiers.Login)]
        public IdentifiedData Create(string resourceType, IdentifiedData body)
        {
            try
            {
                var handler = ResourceHandlerUtil.Current.GetResourceHandler(resourceType);
                if (handler != null)
                {

                    var retVal = handler.Create(body, false);

                    var versioned = retVal as IVersionedEntity;
                    WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.Created;
                    if(versioned != null)
                        WebOperationContext.Current.OutgoingResponse.Headers.Add(HttpResponseHeader.ContentLocation, String.Format("{0}/{1}/{2}/history/{3}",
                            WebOperationContext.Current.IncomingRequest.UriTemplateMatch.BaseUri,
                            resourceType,
                            retVal.Key,
                            versioned.Key));
                    else
                        WebOperationContext.Current.OutgoingResponse.Headers.Add(HttpResponseHeader.ContentLocation, String.Format("{0}/{1}/{2}",
                            WebOperationContext.Current.IncomingRequest.UriTemplateMatch.BaseUri,
                            resourceType,
                            retVal.Key));

                    return retVal;
                }
                else
                    throw new FileNotFoundException(resourceType);

            }
            catch (Exception e)
            {
                this.m_traceSource.TraceEvent(TraceEventType.Error, e.HResult, e.ToString());

                return this.ErrorHelper(e, false);
            }
        }

        /// <summary>
        /// Create or update the specified object
        /// </summary>
        [PolicyPermission(SecurityAction.Demand, PolicyId = PermissionPolicyIdentifiers.Login)]
        public IdentifiedData CreateUpdate(string resourceType, string id, IdentifiedData body)
        {
            try
            {
                var handler = ResourceHandlerUtil.Current.GetResourceHandler(resourceType);
                if (handler != null)
                {

                    var retVal = handler.Create(body, true);

                    var versioned = retVal as IVersionedEntity;
                    WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.Created;
                    if (versioned != null)
                        WebOperationContext.Current.OutgoingResponse.Headers.Add(HttpResponseHeader.ContentLocation, String.Format("{0}/{1}/{2}/history/{3}",
                            WebOperationContext.Current.IncomingRequest.UriTemplateMatch.BaseUri,
                            resourceType,
                            retVal.Key,
                            versioned.Key));
                    else
                        WebOperationContext.Current.OutgoingResponse.Headers.Add(HttpResponseHeader.ContentLocation, String.Format("{0}/{1}/{2}",
                            WebOperationContext.Current.IncomingRequest.UriTemplateMatch.BaseUri,
                            resourceType,
                            retVal.Key));

                    return retVal;
                }
                else
                    throw new FileNotFoundException(resourceType);

            }
            catch (Exception e)
            {
                this.m_traceSource.TraceEvent(TraceEventType.Error, e.HResult, e.ToString());

                return this.ErrorHelper(e, false);
            }
        }

        /// <summary>
        /// Get the specified object
        /// </summary>
        [PolicyPermission(SecurityAction.Demand, PolicyId = PermissionPolicyIdentifiers.Login)]
        public IdentifiedData Get(string resourceType, string id)
        {

            try
            {


                var handler = ResourceHandlerUtil.Current.GetResourceHandler(resourceType);
                if (handler != null)
                {
                    var retVal = handler.Get(Guid.Parse(id), Guid.Empty);
                    if (retVal == null)
                        throw new FileNotFoundException(id);

                    WebOperationContext.Current.OutgoingResponse.ETag = retVal.Tag;
                    WebOperationContext.Current.OutgoingResponse.LastModified = retVal.ModifiedOn.DateTime;

                    // HTTP IF headers?
                    if(WebOperationContext.Current.IncomingRequest.IfModifiedSince.HasValue && 
                        retVal.ModifiedOn <= WebOperationContext.Current.IncomingRequest.IfModifiedSince ||
                        WebOperationContext.Current.IncomingRequest.IfNoneMatch?.Any(o=>retVal.Tag == o) == true)
                    {
                        WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.NotModified;
                        return null;
                    }
                    else if (WebOperationContext.Current.IncomingRequest.UriTemplateMatch.QueryParameters["_bundle"] == "true")
                        return Bundle.CreateBundle(retVal);
                    else
                    {
                        return retVal;
                    }
                }
                else
                    throw new FileNotFoundException(resourceType);

            }
            catch(Exception e)
            {
                this.m_traceSource.TraceEvent(TraceEventType.Error, e.HResult, e.ToString());
                return this.ErrorHelper(e, false);
            }
        }

        /// <summary>
        /// Gets a specific version of a resource
        /// </summary>
        [PolicyPermission(SecurityAction.Demand, PolicyId = PermissionPolicyIdentifiers.Login)]
        public IdentifiedData GetVersion(string resourceType, string id, string versionId)
        {
            try
            {
                var handler = ResourceHandlerUtil.Current.GetResourceHandler(resourceType);
                if (handler != null)
                {
                    var retVal = handler.Get(Guid.Parse(id), Guid.Parse(versionId));
                    if (retVal == null)
                        throw new FileNotFoundException(id);


                   
                    if (WebOperationContext.Current.IncomingRequest.UriTemplateMatch.QueryParameters["_bundle"] == "true")
                        return Bundle.CreateBundle(retVal);
                    else
                        return retVal;
                }
                else
                    throw new FileNotFoundException(resourceType);

            }
            catch (Exception e)
            {
                this.m_traceSource.TraceEvent(TraceEventType.Error, e.HResult, e.ToString());

                return this.ErrorHelper(e, false);
            }
        }

        /// <summary>
        /// Get the schema which defines this service
        /// </summary>
        [PolicyPermission(SecurityAction.Demand, PolicyId = PermissionPolicyIdentifiers.UnrestrictedAdministration)]
        public XmlSchema GetSchema(int schemaId)
        {
            try
            {
                XmlSchemas schemaCollection = new XmlSchemas();

                XmlReflectionImporter importer = new XmlReflectionImporter("http://openiz.org/model");
                XmlSchemaExporter exporter = new XmlSchemaExporter(schemaCollection);

                foreach (var cls in typeof(IImsiServiceContract).GetCustomAttributes<ServiceKnownTypeAttribute>().Select(o=>o.Type))
                    exporter.ExportTypeMapping(importer.ImportTypeMapping(cls, "http://openiz.org/model"));

                if (schemaId > schemaCollection.Count)
                {
                    WebOperationContext.Current.OutgoingResponse.StatusCode = System.Net.HttpStatusCode.NotFound;
                    return null;
                }
                else
                {
                    WebOperationContext.Current.OutgoingResponse.StatusCode = System.Net.HttpStatusCode.OK;
                    WebOperationContext.Current.OutgoingResponse.ContentType = "text/xml";
                    return schemaCollection[schemaId];
                }                
            }
            catch(Exception e)
            {
                WebOperationContext.Current.OutgoingResponse.StatusCode = System.Net.HttpStatusCode.InternalServerError;
                this.m_traceSource.TraceEvent(TraceEventType.Error, e.HResult, e.ToString());
                return null;
            }
        }

        /// <summary>
        /// Gets the recent history an object
        /// </summary>
        [PolicyPermission(SecurityAction.Demand, PolicyId = PermissionPolicyIdentifiers.Login)]
        public IdentifiedData History(string resourceType, string id)
        {
            try
            {
                var handler = ResourceHandlerUtil.Current.GetResourceHandler(resourceType);

                if (handler != null)
                {
                    String since = WebOperationContext.Current.IncomingRequest.UriTemplateMatch.QueryParameters["_since"];
                    Guid sinceGuid = since != null ? Guid.Parse(since) : Guid.Empty;

                    // Query 
                    var retVal = handler.Get(Guid.Parse(id), Guid.Empty);
                    var histItm = retVal;
                    while (histItm != null)
                    {
                        histItm = (histItm as IVersionedEntity)?.PreviousVersion as IdentifiedData;

                        // Should we stop fetching?
                        if ((histItm as IVersionedEntity)?.VersionKey == sinceGuid)
                            break;

                    }

                    // Lock the item
                    return Bundle.CreateBundle(histItm);
                }
                else
                    throw new FileNotFoundException(resourceType);
            }
            catch (Exception e)
            {
                this.m_traceSource.TraceEvent(TraceEventType.Error, e.HResult, e.ToString());

                return this.ErrorHelper(e, false);
            }
        }

        /// <summary>
        /// Perform a search on the specified resource type
        /// </summary>
        [PolicyPermissionAttribute(SecurityAction.Demand, PolicyId = PermissionPolicyIdentifiers.Login)]
        public IdentifiedData Search(string resourceType)
        {
            try
            {
                var handler = ResourceHandlerUtil.Current.GetResourceHandler(resourceType);
                if (handler != null)
                {
                    String offset = WebOperationContext.Current.IncomingRequest.UriTemplateMatch.QueryParameters["_offset"],
                        count = WebOperationContext.Current.IncomingRequest.UriTemplateMatch.QueryParameters["_count"];

                    var query = WebOperationContext.Current.IncomingRequest.UriTemplateMatch.QueryParameters.ToQuery();

                    // Modified on?
                    if (WebOperationContext.Current.IncomingRequest.IfModifiedSince.HasValue)
                        query.Add("modifiedOn", ">" + WebOperationContext.Current.IncomingRequest.IfModifiedSince.Value.ToString("o"));

                    int totalResults = 0;

                    IEnumerable<IdentifiedData> retVal = handler.Query(query, Int32.Parse(offset ?? "0"), Int32.Parse(count ?? "100"), out totalResults);
                    WebOperationContext.Current.OutgoingResponse.LastModified = retVal.OrderByDescending(o => o.ModifiedOn).FirstOrDefault()?.ModifiedOn.DateTime ?? DateTime.Now;


                    // Last modification time and not modified conditions
                    if ((WebOperationContext.Current.IncomingRequest.IfModifiedSince.HasValue ||
                        WebOperationContext.Current.IncomingRequest.IfNoneMatch != null) &&
                        totalResults == 0)
                    {
                        WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.NotModified;
                        return null;
                    }
                    else
                    {
                        return BundleUtil.CreateBundle(retVal, totalResults, Int32.Parse(offset ?? "0"), WebOperationContext.Current.IncomingRequest.UriTemplateMatch.QueryParameters["_lean"] != null);
                    }
                }
                else
                    throw new FileNotFoundException(resourceType);
            }
            catch(Exception e)
            {
                this.m_traceSource.TraceEvent(TraceEventType.Error, e.HResult, e.ToString());

                return this.ErrorHelper(e, false);
            }
        }

        
        /// <summary>
        /// Get the server's current time
        /// </summary>
        public DateTime Time()
        {
            return DateTime.Now;
        }

        /// <summary>
        /// Update the specified resource
        /// </summary>
        [PolicyPermission(SecurityAction.Demand, PolicyId = PermissionPolicyIdentifiers.Login)]
        public IdentifiedData Update(string resourceType, string id, IdentifiedData body)
        {
            try
            {
                var handler = ResourceHandlerUtil.Current.GetResourceHandler(resourceType);
                if (handler != null)
                {

                    var retVal = handler.Update(body);

                    var versioned = retVal as IVersionedEntity;
                    WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.OK;
                    if (versioned != null)
                        WebOperationContext.Current.OutgoingResponse.Headers.Add(HttpResponseHeader.ContentLocation, String.Format("{0}/{1}/{2}/history/{3}",
                            WebOperationContext.Current.IncomingRequest.UriTemplateMatch.BaseUri,
                            resourceType,
                            retVal.Key,
                            versioned.Key));
                    else
                        WebOperationContext.Current.OutgoingResponse.Headers.Add(HttpResponseHeader.ContentLocation, String.Format("{0}/{1}/{2}",
                            WebOperationContext.Current.IncomingRequest.UriTemplateMatch.BaseUri,
                            resourceType,
                            retVal.Key));

                    return retVal;
                }
                else
                    throw new FileNotFoundException(resourceType);

            }
            catch (Exception e)
            {
                this.m_traceSource.TraceEvent(TraceEventType.Error, e.HResult, e.ToString());

                return this.ErrorHelper(e, false);
            }
        }

        #region Helper Methods

        /// <summary>
        /// Throw an appropriate exception based on the caught exception
        /// </summary>
        private ErrorResult ErrorHelper(Exception e, bool returnBundle)
        {

            ErrorResult result = new ErrorResult();
            Trace.TraceError(e.ToString());
            result.Details.Add(new ResultDetail(DetailType.Error, e.Message));

            HttpStatusCode retCode = HttpStatusCode.OK;

            if (e is NotSupportedException)
                retCode = System.Net.HttpStatusCode.MethodNotAllowed;
            else if (e is NotImplementedException)
                retCode = System.Net.HttpStatusCode.NotImplemented;
            else if (e is InvalidDataException)
                retCode = HttpStatusCode.BadRequest;
            else if (e is FileLoadException)
                retCode = System.Net.HttpStatusCode.Gone;
            else if (e is FileNotFoundException || e is ArgumentException)
                retCode = System.Net.HttpStatusCode.NotFound;
            else if (e is ConstraintException)
                retCode = (HttpStatusCode)422;
            else
                retCode = System.Net.HttpStatusCode.InternalServerError;

            WebOperationContext.Current.OutgoingResponse.StatusCode = retCode;
            //WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Xml;


            return result;

        }
        
        /// <summary>
        /// Obsolete the specified data
        /// </summary>
        [PolicyPermission(SecurityAction.Demand, PolicyId = PermissionPolicyIdentifiers.Login)]
        public IdentifiedData Delete(string resourceType, string id)
        {
            try
            {
                var handler = ResourceHandlerUtil.Current.GetResourceHandler(resourceType);
                if (handler != null)
                {

                    var retVal = handler.Obsolete(Guid.Parse(id));

                    var versioned = retVal as IVersionedEntity;

                    WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.Created;
                    if (versioned != null)
                        WebOperationContext.Current.OutgoingResponse.Headers.Add(HttpResponseHeader.ContentLocation, String.Format("{0}/{1}/{2}/history/{3}",
                            WebOperationContext.Current.IncomingRequest.UriTemplateMatch.BaseUri,
                            resourceType,
                            retVal.Key,
                            versioned.Key));
                    else
                        WebOperationContext.Current.OutgoingResponse.Headers.Add(HttpResponseHeader.ContentLocation, String.Format("{0}/{1}/{2}",
                            WebOperationContext.Current.IncomingRequest.UriTemplateMatch.BaseUri,
                            resourceType,
                            retVal.Key));

                    return retVal;
                }
                else
                    throw new FileNotFoundException(resourceType);

            }
            catch (Exception e)
            {
                return this.ErrorHelper(e, false);
            }
        }

        /// <summary>
        /// Perform the search but only return the headers
        /// </summary>
        public void HeadSearch(string resourceType)
        {
            WebOperationContext.Current.IncomingRequest.UriTemplateMatch.QueryParameters.Add("_count", "1");
                this.Search(resourceType);
        }

        /// <summary>
        /// Get just the headers
        /// </summary>
        public void GetHead(string resourceType, string id)
        {
            this.Get(resourceType, id);
        }
        #endregion
    }
}