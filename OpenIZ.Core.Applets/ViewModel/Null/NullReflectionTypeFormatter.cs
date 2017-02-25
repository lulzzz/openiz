﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenIZ.Core.Model;
using System.Reflection;
using System.Collections;
using OpenIZ.Core.Model.Attributes;
using OpenIZ.Core.Model.Interfaces;
using OpenIZ.Core.Services;
using Newtonsoft.Json;

namespace OpenIZ.Core.Applets.ViewModel.Null
{
    /// <summary>
    /// Memory based reflection type formatter
    /// </summary>
    public class NullReflectionTypeFormatter : INullTypeFormatter
    {

        // Lock object
        private object m_syncLock = new object();

        // JSON property information
        private Dictionary<Type, Dictionary<String, PropertyInfo>> m_jsonPropertyInfo = new Dictionary<Type, Dictionary<string, PropertyInfo>>();

        // JSON Property names
        private Dictionary<PropertyInfo, String> m_jsonPropertyNames = new Dictionary<PropertyInfo, string>();

        /// <summary>
        /// Constructs a new reflection type formatter
        /// </summary>
        public NullReflectionTypeFormatter(Type type)
        {
            this.HandlesType = type;
        }

        /// <summary>
        /// Gets the type this handles
        /// </summary>
        public Type HandlesType
        {
            get; private set;
        }

        /// <summary>
        /// Get property name
        /// </summary>
        protected String GetPropertyName(PropertyInfo info)
        {

            String retVal = null;
            if (!this.m_jsonPropertyNames.TryGetValue(info, out retVal))
            {
                if (info.GetCustomAttribute<JsonIgnoreAttribute>() != null && info.GetCustomAttribute<SerializationReferenceAttribute>() == null)
                    retVal = null;
                else
                {
                    // Property info
                    JsonPropertyAttribute jpa = info.GetCustomAttribute<JsonPropertyAttribute>();
                    if (jpa != null)
                        retVal = jpa.PropertyName;
                    else
                    {
                        SerializationReferenceAttribute sra = info.GetCustomAttribute<SerializationReferenceAttribute>();
                        if (sra != null)
                        {
                            jpa = info.DeclaringType.GetRuntimeProperty(sra.RedirectProperty).GetCustomAttribute<JsonPropertyAttribute>();
                            if (jpa != null)
                                retVal = jpa.PropertyName + "Model";
                        }
                    }

                    if (retVal == null)
                        retVal = info.Name.ToLower() + "Model";
                }

                lock (this.m_syncLock)
                    if (!this.m_jsonPropertyNames.ContainsKey(info))
                        this.m_jsonPropertyNames.Add(info, retVal);
            }
            return retVal;

        }


        /// <summary>
        /// Serialize the specified instance
        /// </summary>
        public void Serialize(IdentifiedData o, NullSerializationContext context)
        {
            if (o == null)
                throw new ArgumentNullException(nameof(o));

            // For each item in the property ...
            bool loadedProperties = false;

            // Iterate properties 
            foreach (var propertyInfo in o.GetType().GetRuntimeProperties())
            {
                // Get the property name
                var propertyName = GetPropertyName(propertyInfo);
                if (propertyName == null || propertyName.StartsWith("$")) // Skip internal property names
                    continue;

                // Serialization decision
                if (!context.ShouldSerialize(propertyName))
                    continue;

                // Get the property 
                var value = propertyInfo.GetValue(o);

                // Null ,do we want to force load?
                if (value == null || (value as IList)?.Count == 0)
                {
                    var tkey = o.Key.HasValue ? o.Key.Value : Guid.NewGuid();  
                    if (context.ShouldForceLoad(propertyName, tkey))
                    {
                        if (o.Key.HasValue && value is IList && !propertyInfo.PropertyType.IsArray)
                        {
                            if (o.Key.HasValue)
                                value = context.NullContext.LoadCollection(propertyInfo.PropertyType, (Guid)o.Key);
                            propertyInfo.SetValue(o, value);
                            loadedProperties = (value as IList).Count > 0;
                        }
                        else
                        {
                            var keyPropertyRef = propertyInfo.GetCustomAttribute<SerializationReferenceAttribute>();
                            var keyProperty = o.GetType().GetRuntimeProperty(keyPropertyRef.RedirectProperty);
                            var key = keyProperty.GetValue(o);
                            if (key != null)
                            {
                                value = context.NullContext.LoadRelated(propertyInfo.PropertyType, (Guid)key);
                                propertyInfo.SetValue(o, value);
                                loadedProperties = value != null;
                            }

                        }

                    }
                    else
                        continue;
                }

                // TODO: Classifier
                context.NullContext.WritePropertyUtil(propertyName, value, context);

            }
            
    }
}
}
