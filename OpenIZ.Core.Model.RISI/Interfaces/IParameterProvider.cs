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
 * Date: 2017-1-5
 */

using System;

namespace OpenIZ.Core.Model.RISI.Interfaces
{
	/// <summary>
	/// Represents a parameter provider.
	/// </summary>
	public interface IParameterProvider
	{
		/// <summary>
		/// Gets a value for a given parameter.
		/// </summary>
		/// <typeparam name="T">The type of parameter to retrieve.</typeparam>
		/// <param name="id">The id of the parameter.</param>
		/// <returns>Returns the value of the parameter.</returns>
		T GetValue<T>(Guid id);
	}
}