﻿using Newtonsoft.Json;
using OpenIZ.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenIZ.Core.Model.Extensions
{
    /// <summary>
    /// Extension that emits data as kvp
    /// </summary>
    public class DictionaryExtensionHandler : IExtensionHandler
    {
        /// <summary>
        /// Gets the name of the extension
        /// </summary>
        public string Name
        {
            get
            {
                return "Dictionary";
            }
        }

        /// <summary>
        /// Deserialize data from the extension
        /// </summary>
        public object DeSerialize(byte[] extensionData)
        {
            JsonSerializer jsz = new JsonSerializer();
            using (var ms = new MemoryStream(extensionData))
            using (var tr = new StreamReader(ms))
            using (var jr = new JsonTextReader(tr))
                return jsz.Deserialize(jr);
        }

        /// <summary>
        /// Get display value
        /// </summary>
        public string GetDisplay(object data)
        {
            return JsonConvert.SerializeObject(data);
        }

        /// <summary>
        /// Serialize actual data
        /// </summary>
        public byte[] Serialize(object data)
        {
            JsonSerializer jsz = new JsonSerializer();
            using (var ms = new MemoryStream())
            {
                using (var tr = new StreamWriter(ms))
                using (var jr = new JsonTextWriter(tr))
                    jsz.Serialize(jr, data);
                return ms.ToArray();
            }
        }
    }
}