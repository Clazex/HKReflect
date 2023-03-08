using System.Collections.Generic;
using System.Linq;

using Fody;

using Mono.Cecil;

namespace HKReflect.Fody;

public sealed partial class ModuleWeaver : BaseModuleWeaver {
	public override bool ShouldCleanReference => true;

	public override IEnumerable<string> GetAssembliesForScanning() {
		yield return "Assembly-CSharp";
	}

	public override void Execute() {
		if (!ModuleDefinition.AssemblyReferences.Any(asmRef => asmRef.Name == "Assembly-CSharp")) {
			WriteError("The assembly is not referencing Assembly-CSharp, skipping substitution");
			return;
		}

		if (!ModuleDefinition.AssemblyReferences.Any(asmRef => asmRef.Name == "HKReflect")) {
			WriteWarning("The assembly is not referencing HKReflect, skipping substitution");
			return;
		}

		EnsureSkippedVisibilityCheck(ModuleDefinition);

		ModuleDefinition.Types.ParallelForEach(ProcessType);
	}
}
