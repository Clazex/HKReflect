using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Mono.Cecil;
using Mono.Cecil.Cil;

namespace ReflectGen;

internal sealed partial class Program {
	private static TypeDefinition? GenerateInstanceClass(
		TypeDefinition typeDef,
		ModuleDefinition module,
		string baseNs,
		TypeDefinition reflectorType,
		Dictionary<TypeDefinition, string> typesToAddInheritance
	) {
		if (typeDef.BaseType is TypeReference baseType) {
			TypeDefinition type = baseType.Resolve();

			while (type.BaseType != null && type.BaseType.FullName != typeof(object).FullName) {
				type = type.BaseType.Resolve();
			}

			if (type.FullName == typeof(Attribute).FullName) {
				return null;
			}
		}

		Lazy<TypeDefinition> resTypeDef = new(() => new(
			typeDef.IsNested
				? string.Empty
				: string.IsNullOrEmpty(typeDef.Namespace) ? baseNs : $"{baseNs}.{typeDef.Namespace}",
			typeDef.Name,
			(typeDef.Attributes & (~TypeAttributes.NestedFamORAssem))
				| (typeDef.IsNested ? TypeAttributes.NestedPublic : TypeAttributes.Public)
		));

		Lazy<ILProcessor> fieldMap = new(() => {
			MethodDefinition fieldMapMethod = new(
				"<OrigFields>",
				MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.SpecialName,
				module.TypeSystem.Void
			);

			resTypeDef.Value.Methods.Add(fieldMapMethod);

			return fieldMapMethod.Body.GetILProcessor();
		});

		Lazy<ILProcessor> methodMap = new(() => {
			MethodDefinition methodMapMethod = new(
				"<OrigMethods>",
				MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.SpecialName,
				module.TypeSystem.Void
			);

			resTypeDef.Value.Methods.Add(methodMapMethod);

			return methodMapMethod.Body.GetILProcessor();
		});

		foreach (FieldDefinition fieldDef in typeDef.Fields) {
			if (IsCompilerGenerated(fieldDef) || !IsPubliclyAvailable(fieldDef)) {
				continue;
			}

			resTypeDef.Value.Fields.Add(new(
				fieldDef.Name,
				FieldAttributes.Public | (fieldDef.IsStatic ? FieldAttributes.Static : FieldAttributes.CompilerControlled),
				module.ImportReference(fieldDef.FieldType)
			));

			fieldMap.Value.Emit(OpCodes.Ldstr, fieldDef.Name);
			fieldMap.Value.Emit(OpCodes.Stfld, module.ImportReference(fieldDef));
		}

		foreach (PropertyDefinition propDef in typeDef.Properties) {
			if (!IsPubliclyAvailable(propDef)) {
				continue;
			}

			PropertyDefinition resPropDef = new(
				propDef.Name,
				propDef.HasDefault ? PropertyAttributes.HasDefault : PropertyAttributes.None,
				module.ImportReference(propDef.PropertyType)
			);

			if (propDef.GetMethod != null) {
				MethodDefinition get = new(
					propDef.GetMethod.Name,
					(propDef.GetMethod.Attributes & (~MethodAttributes.MemberAccessMask)) | MethodAttributes.Public,
					module.ImportReference(propDef.PropertyType)
				) {
					IsSpecialName = true
				};

				resTypeDef.Value.Methods.Add(get);
				resPropDef.GetMethod = get;

				methodMap.Value.Emit(OpCodes.Ldstr, get.FullName);
				methodMap.Value.Emit(OpCodes.Call, module.ImportReference(propDef.GetMethod));
			}

			if (propDef.SetMethod != null) {
				MethodDefinition set = new(
					propDef.SetMethod.Name,
					(propDef.SetMethod.Attributes & (~MethodAttributes.MemberAccessMask)) | MethodAttributes.Public,
					module.TypeSystem.Void
				) {
					IsSpecialName = true
				};

				set.Parameters.Add(new(module.ImportReference(propDef.PropertyType)));

				resTypeDef.Value.Methods.Add(set);
				resPropDef.SetMethod = set;

				methodMap.Value.Emit(OpCodes.Ldstr, set.FullName);
				methodMap.Value.Emit(OpCodes.Call, module.ImportReference(propDef.SetMethod));
			}

			resTypeDef.Value.Properties.Add(resPropDef);
		}

		foreach (MethodDefinition methodDef in typeDef.Methods) {
			if (methodDef.HasGenericParameters) {
				if (!methodDef.IsPublic) {
					Console.WriteLine($"Skipping method {methodDef.FullName} because generic methods are not supported");
				}

				continue;
			}

			if (
				methodDef.IsSpecialName
				|| IsCompilerGenerated(methodDef)
				|| !IsPubliclyAvailable(methodDef)
			) {
				continue;
			}

			MethodDefinition resMethodDef = new(
				methodDef.Name,
				(methodDef.Attributes & (~MethodAttributes.MemberAccessMask)) | MethodAttributes.Public,
				module.ImportReference(methodDef.ReturnType)
			);

			foreach (ParameterDefinition origPd in methodDef.Parameters) {
				ParameterDefinition pd = new(origPd.Name, origPd.Attributes, module.ImportReference(origPd.ParameterType));
				foreach (CustomAttribute attr in origPd.CustomAttributes) {
					pd.CustomAttributes.Add(new(module.ImportReference(attr.Constructor), attr.GetBlob()));
				}

				resMethodDef.Parameters.Add(pd);
			}

			resTypeDef.Value.Methods.Add(resMethodDef);

			methodMap.Value.Emit(OpCodes.Ldstr, resMethodDef.FullName);
			methodMap.Value.Emit(OpCodes.Call, module.ImportReference(methodDef));
		}

		foreach (TypeDefinition nestedType in typeDef.NestedTypes) {
			if (GenerateClass(nestedType, module, baseNs, reflectorType, typesToAddInheritance) is TypeDefinition type) {
				resTypeDef.Value.NestedTypes.Add(type);
			}
		}

		if (resTypeDef.IsValueCreated) {
			TypeDefinition resTypeDefVal = resTypeDef.Value;

			fieldMap.Value.Emit(OpCodes.Ret);
			methodMap.Value.Emit(OpCodes.Ret);

			MethodDefinition reflectMethodDef = new("Reflect", MethodAttributes.Public | MethodAttributes.Static, resTypeDefVal);
			reflectMethodDef.Parameters.Add(new("self", ParameterAttributes.None, module.ImportReference(typeDef)));
			reflectMethodDef.CustomAttributes.Add(new(module.ImportReference(typeof(ExtensionAttribute).GetConstructor(Type.EmptyTypes))));
			reflectorType.Methods.Add(reflectMethodDef);

			if (typeDef.BaseType != null && typeDef.BaseType.FullName != typeof(object).FullName) {
				typesToAddInheritance.Add(
					resTypeDefVal,
					$"{baseNs}.{typeDef.BaseType.FullName}"
				);
			}

			return resTypeDefVal;
		}

		return null;
	}

}
