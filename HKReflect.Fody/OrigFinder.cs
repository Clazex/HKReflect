using System.Linq;

using Mono.Cecil;

namespace HKReflect.Fody;

public sealed partial class ModuleWeaver {
	private TypeDefinition FindOrigType(TypeReference typeRef) =>
		FindTypeDefinition(typeRef.FullName.StripStart("HKReflect").StripStart("."));

	private TypeDefinition FindOrigTypeStatic(TypeReference typeRef) => FindTypeDefinition(
		typeRef.FullName.StripStart("HKReflect.Static").StripStart(".").StripEnd("R")
	);

	private TypeDefinition FindOrigTypeSingleton(TypeReference typeRef) => FindTypeDefinition(
		typeRef.FullName.StripStart("HKReflect.Singleton").StripStart(".").StripEnd("R")
	);

	private FieldDefinition FindOrigField(TypeDefinition origType, FieldReference fieldRef) =>
		origType.Fields.First(fieldDef => fieldDef.Name == fieldRef.Name);

	private MethodDefinition FindOrigMethod(TypeDefinition origType, MethodReference methodRef) =>
		origType.Methods.First(methodDef => methodDef.Name == methodRef.Name);
}
