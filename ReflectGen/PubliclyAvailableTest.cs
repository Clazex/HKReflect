using System.Linq;

using Mono.Cecil;

namespace ReflectGen;

internal sealed partial class Program {
	private static bool IsPubliclyAvailable(TypeDefinition typeDef) => typeDef.IsGenericParameter
		|| (typeDef.IsPublic ^ typeDef.IsNestedPublic);

	private static bool IsPubliclyAvailable(TypeReference typeRef) => typeRef.IsGenericParameter ||
		(typeRef.Resolve() is TypeDefinition typeDef && IsPubliclyAvailable(typeDef));

	private static bool IsPubliclyAvailable(FieldDefinition fieldDef) => !fieldDef.IsLiteral
		&& !fieldDef.IsInitOnly
		&& IsPubliclyAvailable(fieldDef.FieldType);

	private static bool IsPubliclyAvailable(PropertyDefinition propDef) => IsPubliclyAvailable(propDef.PropertyType);

	private static bool IsPubliclyAvailable(MethodDefinition methodDef) => IsPubliclyAvailable(methodDef.ReturnType)
		&& methodDef.Parameters.All(IsPubliclyAvailable)
		&& methodDef.GenericParameters.All(IsPubliclyAvailable);

	private static bool IsPubliclyAvailable(ParameterDefinition paramDef) => IsPubliclyAvailable(paramDef.ParameterType);

	private static bool IsPubliclyAvailable(GenericParameter genericPar) => genericPar.Constraints.All(IsPubliclyAvailable);

	private static bool IsPubliclyAvailable(GenericParameterConstraint constraint) => IsPubliclyAvailable(constraint.ConstraintType);
}
