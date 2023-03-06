using Mono.Cecil;

namespace ReflectGen;

internal sealed partial class Program {
	private static void GenerateSingletonAccess(TypeDefinition mappedType, TypeDefinition singletonsType) {
		string name = mappedType.Name + 'R';

		MethodDefinition getter = new(
			"get_" + name,
			MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.SpecialName,
			mappedType
		);
		singletonsType.Methods.Add(getter);

		singletonsType.Properties.Add(new(name, PropertyAttributes.None, mappedType) {
			GetMethod = getter
		});
	}
}
