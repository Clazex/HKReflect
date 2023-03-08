using System.Collections.Generic;
using System.Linq;

using Mono.Cecil;
using Mono.Cecil.Cil;

namespace HKReflect.Fody;

public sealed partial class ModuleWeaver {
	private static readonly Dictionary<string, (Dictionary<string, FieldReference> fieldMap, Dictionary<string, MethodReference> methodMap)> origMap = new();

	private static void BuildOrigMap(
		TypeDefinition type,
		out Dictionary<string, FieldReference> fieldMap,
		out Dictionary<string, MethodReference> methodMap
	) {
		fieldMap = new();
		methodMap = new();

		Instruction[] rawFieldMap = type.Methods.Single(method => method.Name == "<OrigFields>")
			.Body.Instructions.ToArray();

		for (int i = 0; i < rawFieldMap.Length; i += 2) {
			if (rawFieldMap[i].OpCode.Code == Code.Ret) {
				break;
			}

			fieldMap.Add((string) rawFieldMap[i].Operand, (FieldReference) rawFieldMap[i + 1].Operand);
		}

		Instruction[] rawMethodMap = type.Methods.Single(method => method.Name == "<OrigMethods>")
			.Body.Instructions.ToArray();

		for (int i = 0; i < rawMethodMap.Length; i += 2) {
			if (rawMethodMap[i].OpCode.Code == Code.Ret) {
				break;
			}

			methodMap.Add((string) rawMethodMap[i].Operand, (MethodReference) rawMethodMap[i + 1].Operand);
		}
	}

	private void GetOrigMap(
		TypeReference type,
		out Dictionary<string, FieldReference> fieldMap,
		out Dictionary<string, MethodReference> methodMap
	) {
		string fullName = type.FullName;

		lock (origMap) {
			if (origMap.ContainsKey(fullName)) {
				(fieldMap, methodMap) = origMap[fullName];
			} else {
				BuildOrigMap(type as TypeDefinition ?? type.Resolve(), out fieldMap, out methodMap);
				origMap[fullName] = (fieldMap, methodMap);
			}
		}
	}

	private TypeDefinition FindOrigType(TypeReference typeRef) => FindTypeDefinition(
		typeRef.FullName.StripStart("HKReflect").StripStart(".")
	);

	private FieldReference FindOrigField(TypeReference type, FieldReference fieldRef) {
		GetOrigMap(type, out Dictionary<string, FieldReference> fieldMap, out _);
		return fieldMap[fieldRef.Name];
	}

	private MethodReference FindOrigMethod(TypeReference type, MethodReference methodRef) {
		GetOrigMap(type, out _, out Dictionary<string, MethodReference> methodMap);
		return methodMap[methodRef.GetElementMethod().FullName];
	}
}
