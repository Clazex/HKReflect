using System;
using System.Collections.Generic;

using Mono.Cecil;

namespace ReflectGen;

internal sealed partial class Program {
	private static TypeDefinition? GenerateClass(
		TypeDefinition typeDef,
		ModuleDefinition module,
		string baseNs,
		TypeDefinition reflectorType,
		Dictionary<TypeDefinition, string> typesToAddInheritance
	) {
		if (typeDef.IsValueType || typeDef.IsInterface || !IsPubliclyAvailable(typeDef)) {
			return null;
		}

		if (typeDef.HasGenericParameters) {
			Console.WriteLine($"Skipping type {typeDef.FullName} because generic types are not supported");
			return null;
		}

		return typeDef.IsAbstract && typeDef.IsSealed
			? GenerateStaticClass(typeDef, module, baseNs, reflectorType, typesToAddInheritance)
			: GenerateInstanceClass(typeDef, module, baseNs, reflectorType, typesToAddInheritance);
	}
}
