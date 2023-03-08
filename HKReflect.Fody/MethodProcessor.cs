using System.Collections.Generic;
using System.Linq;

using Fody;

using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace HKReflect.Fody;

public sealed partial class ModuleWeaver {
	private void ProcessMethod(MethodDefinition methodDef, TypeDefinition typeDef) {
		foreach (GenericParameter gp in methodDef.GenericParameters) {
			foreach (GenericParameterConstraint constraint in gp.Constraints) {
				if (constraint.ConstraintType.IsHKReflectType()) {
					throw new WeavingException(methodDef.FullName + " contains generic constraint(s) of reflected type, please use original type instead");
				}
			}
		}

		foreach (ParameterDefinition paramDef in methodDef.Parameters) {
			if (paramDef.ParameterType.IsHKReflectType()) {
				throw new WeavingException(methodDef.FullName + " contains parameter(s) of reflected type, please use original type instead");
			}
		}

		if (!methodDef.HasBody) {
			return;
		}

		MethodBody body = methodDef.Body;

		Instruction[] branchInsts = body.Instructions
			.Where(i => i.OpCode.FlowControl is FlowControl.Branch or FlowControl.Cond_Branch)
			.ToArray();

		foreach (VariableDefinition varDef in body.Variables) {
			if (varDef.VariableType.IsHKReflectType()) {
				varDef.VariableType = ModuleDefinition.ImportReference(FindOrigType(varDef.VariableType));
			}
		}

		body.SimplifyMacros();

		foreach (Instruction inst in body.Instructions.ToArray()) {
			ProcessInstruction(inst, methodDef, typeDef, branchInsts);
		}

		body.Optimize();
	}
}
