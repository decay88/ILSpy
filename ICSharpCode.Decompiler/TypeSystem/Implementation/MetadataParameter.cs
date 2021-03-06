﻿// Copyright (c) 2018 Daniel Grunwald
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using ICSharpCode.Decompiler.Util;

namespace ICSharpCode.Decompiler.TypeSystem.Implementation
{
	sealed class MetadataParameter : IParameter
	{
		readonly MetadataModule module;
		readonly ParameterHandle handle;
		readonly ParameterAttributes attributes;

		public IType Type { get; }
		public IParameterizedMember Owner { get; }

		// lazy-loaded:
		string name;

		internal MetadataParameter(MetadataModule module, IParameterizedMember owner, IType type, ParameterHandle handle)
		{
			this.module = module;
			this.Owner = owner;
			this.Type = type;
			this.handle = handle;

			var param = module.metadata.GetParameter(handle);
			this.attributes = param.Attributes;
		}

		public EntityHandle MetadataToken => handle;

		#region Attributes
		public IEnumerable<IAttribute> GetAttributes()
		{
			var b = new AttributeListBuilder(module);
			var metadata = module.metadata;
			var parameter = metadata.GetParameter(handle);

			if (!IsOut) {
				if ((attributes & ParameterAttributes.In) == ParameterAttributes.In)
					b.Add(KnownAttribute.In);
				if ((attributes & ParameterAttributes.Out) == ParameterAttributes.Out)
					b.Add(KnownAttribute.Out);
			}
			b.Add(parameter.GetCustomAttributes());
			b.AddMarshalInfo(parameter.GetMarshallingDescriptor());

			return b.Build();
		}
		#endregion

		const ParameterAttributes inOut = ParameterAttributes.In | ParameterAttributes.Out;
		public bool IsRef => Type.Kind == TypeKind.ByReference && (attributes & inOut) != ParameterAttributes.Out;
		public bool IsOut => Type.Kind == TypeKind.ByReference && (attributes & inOut) == ParameterAttributes.Out;
		public bool IsOptional => (attributes & ParameterAttributes.Optional) != 0;

		public bool IsParams {
			get {
				if (Type.Kind != TypeKind.Array)
					return false;
				var metadata = module.metadata;
				var propertyDef = metadata.GetParameter(handle);
				return propertyDef.GetCustomAttributes().HasKnownAttribute(metadata, KnownAttribute.ParamArray);
			}
		}

		public string Name {
			get {
				string name = LazyInit.VolatileRead(ref this.name);
				if (name != null)
					return name;
				var metadata = module.metadata;
				var propertyDef = metadata.GetParameter(handle);
				return LazyInit.GetOrSet(ref this.name, metadata.GetString(propertyDef.Name));
			}
		}

		bool IVariable.IsConst => false;

		public object ConstantValue {
			get {
				var metadata = module.metadata;
				var propertyDef = metadata.GetParameter(handle);
				var constantHandle = propertyDef.GetDefaultValue();
				if (constantHandle.IsNil)
					return null;
				var constant = metadata.GetConstant(constantHandle);
				var blobReader = metadata.GetBlobReader(constant.Value);
				return blobReader.ReadConstant(constant.TypeCode);
			}
		}

		SymbolKind ISymbol.SymbolKind => SymbolKind.Parameter;

		public override string ToString()
		{
			return $"{MetadataTokens.GetToken(handle):X8} {DefaultParameter.ToString(this)}";
		}
	}
}
