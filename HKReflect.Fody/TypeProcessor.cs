using Mono.Cecil;

namespace HKReflect.Fody;

public sealed partial class ModuleWeaver {
	private void ProcessType(TypeDefinition typeDef) {
		if (typeDef.IsInNamespace(nameof(System))) { // Polyfill types
			return;
		}

		typeDef.Fields.ForEach(ProcessField);

		typeDef.Methods.ParallelForEach(method => ProcessMethod(method, typeDef));
	}
}
