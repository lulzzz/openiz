﻿/*
 * Copyright 2015-2017 Mohawk College of Applied Arts and Technology
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
 * User: khannan
 * Date: 2017-4-4
 */

using OpenIZ.OrmLite.Attributes;
using System;

namespace OpenIZ.Persistence.Reporting.PSQL.Model
{
	/// <summary>
	/// Represents a parameter type.
	/// </summary>
	[Table("parameter_type")]
	public class ParameterType : DbIdentified
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ParameterType"/> class.
		/// </summary>
		public ParameterType() : base(Guid.NewGuid())
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ParameterType"/> class.
		/// </summary>
		/// <param name="key">The key.</param>
		public ParameterType(Guid key) : base(key)
		{
			this.CreationTime = DateTimeOffset.UtcNow;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ParameterType"/> class
		/// with a specific name.
		/// </summary>
		/// <param name="type">The type of parameter.</param>
		public ParameterType(string type) : this(Guid.NewGuid())
		{
			this.Type = type;
		}

		/// <summary>
		/// Gets or sets the creation time of the parameter type.
		/// </summary>
		[NotNull]
		[Column("creation_time")]
		public DateTimeOffset CreationTime { get; set; }

		/// <summary>
		/// Gets or sets the key.
		/// </summary>
		/// <value>The key.</value>
		[PrimaryKey]
		[Column("id")]
		[AutoGenerated]
		public override Guid Key { get; set; }

		/// <summary>
		/// Gets or sets the type of the parameter type.
		/// </summary>
		[NotNull]
		[Column("type")]
		public string Type { get; set; }

		/// <summary>
		/// Gets or sets the values provider of the parameter type.
		/// </summary>
		[Column("values_provider")]
		public string ValuesProvider { get; set; }
	}
}