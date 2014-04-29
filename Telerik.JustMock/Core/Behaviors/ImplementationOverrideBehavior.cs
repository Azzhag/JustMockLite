/*
 JustMock Lite
 Copyright © 2010-2014 Telerik AD

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

     http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/

using System;
using System.Linq;

namespace Telerik.JustMock.Core.Behaviors
{
	internal class ImplementationOverrideBehavior : IBehavior
	{
		private static readonly object[] Empty = new object[0];

		private readonly Delegate implementationOverride;
		private readonly bool ignoreDelegateReturnValue;
		private readonly Delegate overrideInvoker;

		public ImplementationOverrideBehavior(Delegate implementationOverride, bool ignoreDelegateReturnValue)
		{
			this.ignoreDelegateReturnValue = ignoreDelegateReturnValue;
			this.implementationOverride = implementationOverride;

			this.overrideInvoker = implementationOverride.Method.ReturnType != typeof(void)
				? (Delegate) MockingUtil.MakeFuncCaller(implementationOverride)
				: MockingUtil.MakeProcCaller(implementationOverride);
		}

		public object CallOverride(Invocation invocation)
		{
			var args = implementationOverride.Method.GetParameters().Length > 0 && invocation.Args != null ? invocation.Args : Empty;

			var paramsCount = invocation.Method.GetParameters().Length;
			var implementationParamsCount = implementationOverride.Method.GetParameters().Length;

			if (invocation.Method.IsExtensionMethod() && paramsCount - 1 == implementationParamsCount)
			{
				args = args.Skip(1).ToArray();
			}

			int extraParamCount = 1 + (implementationOverride.Target != null && implementationOverride.Method.IsStatic ? 1 : 0);
			if (!invocation.Method.IsStatic && extraParamCount + paramsCount == implementationParamsCount)
			{
				args = new[] { invocation.Instance }.Concat(args).ToArray();
			}

			try
			{
				object returnValue = null;
				if (implementationOverride.Method.ReturnType != typeof(void))
				{
					var invoker = (Func<object[], Delegate, object>)this.overrideInvoker;
					returnValue = ProfilerInterceptor.GuardExternal(() => invoker(args, this.implementationOverride));
				}
				else
				{
					var invoker = (Action<object[], Delegate>)this.overrideInvoker;
					ProfilerInterceptor.GuardExternal(() => invoker(args, this.implementationOverride));
				}
				return returnValue;
			}
			catch (InvalidCastException ex)
			{
				throw new MockException("The implementation callback has an incorrect signature", ex);
			}
		}

		public void Process(Invocation invocation)
		{
			var returnValue = CallOverride(invocation);
			if (implementationOverride.Method.ReturnType != typeof(void) && !this.ignoreDelegateReturnValue)
				invocation.ReturnValue = returnValue;
			invocation.UserProvidedImplementation = true;
		}
	}
}
