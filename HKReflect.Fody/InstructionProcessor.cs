using System;
using System.Linq;

using Fody;

using Mono.Cecil;
using Mono.Cecil.Cil;

namespace HKReflect.Fody;

public sealed partial class ModuleWeaver {
	private void ProcessInstruction(
		Instruction inst,
		MethodDefinition methodDef,
		TypeDefinition typeDef,
		Instruction[] branchInsts
	) {
		if (inst.Operand is TypeReference typeRef && typeRef.IsHKReflectType()) {
			throw new WeavingException(methodDef.FullName + " contains typeof on HKReflect types");
		} else if (inst.Operand is FieldReference fieldRef && fieldRef.DeclaringType.IsHKReflectType()) {
			ProcessFieldAccess(inst, fieldRef, methodDef, typeDef, branchInsts);
		} else if (inst.Operand is MethodReference methodRef && methodRef.DeclaringType.IsHKReflectType()) {
			if (methodRef.DeclaringType.FullName == "HKReflect.Reflector") {
				Reroute(branchInsts, inst, inst.Next);
				methodDef.Body.GetILProcessor().Remove(inst);
			} else if (methodRef.DeclaringType.FullName == "HKReflect.Singletons") {
				Instruction accessInst = CreateSingletonInstanceGetInstruction(methodRef.ReturnType);
				inst.OpCode = accessInst.OpCode;
				inst.Operand = accessInst.Operand;
			} else {
				ProcessMethodCall(inst, methodRef);
			}
		}
	}

	private void ProcessFieldAccess(Instruction inst, FieldReference fieldRef, MethodDefinition methodDef, TypeDefinition typeDef, Instruction[] branchInsts) {
		if (fieldRef.DeclaringType.FullName == "HKReflect.PlayerData" && inst.OpCode.Code is Code.Ldfld or Code.Stfld) {
			ProcessFieldAccessPlayerData(inst, fieldRef, methodDef, typeDef, branchInsts);
			return;
		}

		inst.Operand = ModuleDefinition.ImportReference(FindOrigField(fieldRef.DeclaringType, fieldRef));
	}

	private void ProcessFieldAccessPlayerData(Instruction inst, FieldReference fieldRef, MethodDefinition methodDef, TypeDefinition typeDef, Instruction[] branchInsts) {
		TypeDefinition pdType = FindTypeDefinition("PlayerData");

		MethodReference methodRef = inst.OpCode.Code switch {
			Code.Ldfld => fieldRef.FieldType.FullName switch {
				"System.Boolean" => pdType.Methods.First(method => method.Name == "GetBool"),
				"System.Int32" => pdType.Methods.First(method => method.Name == "GetInt"),
				"System.Single" => pdType.Methods.First(method => method.Name == "GetFloat"),
				"UnityEngine.Vector3" => pdType.Methods.First(method => method.Name == "GetVector3"),
				_ => pdType.Methods.First(method => method.Name == "GetVariable")
					.MakeGenericMethod(ModuleDefinition.ImportReference(fieldRef.FieldType)),
			},
			Code.Stfld => fieldRef.FieldType.FullName switch {
				"System.Boolean" => pdType.Methods.First(method => method.Name == "SetBoolSwappedArgs"),
				"System.Int32" => pdType.Methods.First(method => method.Name == "SetIntSwappedArgs"),
				"System.Single" => pdType.Methods.First(method => method.Name == "SetFloatSwappedArgs"),
				"UnityEngine.Vector3" => pdType.Methods.First(method => method.Name == "SetVector3SwappedArgs"),
				_ => pdType.Methods.First(method => method.Name == "SetVariableSwappedArgs")
					.MakeGenericMethod(ModuleDefinition.ImportReference(fieldRef.FieldType)),
			},
			Code code => throw new WeavingException($"{methodDef.FullName} contains invalid opcode {code} for accessing field {fieldRef.FullName}")
		};

		var ldFldNameInst = Instruction.Create(OpCodes.Ldstr, fieldRef.Name);
		methodDef.Body.GetILProcessor().InsertBefore(inst, ldFldNameInst);
		Reroute(branchInsts, inst, ldFldNameInst);
		inst.OpCode = OpCodes.Callvirt;
		inst.Operand = ModuleDefinition.ImportReference(methodRef);
	}


	private void ProcessMethodCall(Instruction inst, MethodReference methodRef) =>
		inst.Operand = ModuleDefinition.ImportReference(FindOrigMethod(methodRef.DeclaringType, methodRef));


	private Instruction CreateSingletonInstanceGetInstruction(TypeReference typeRef) => typeRef.FullName switch {
		"HKReflect.GameCameras" => Instruction.Create(
			OpCodes.Call,
			ModuleDefinition.ImportReference(FindTypeDefinition("GameCameras").Methods
				.First(methodDef => methodDef.Name == "get_instance")
			)
		),
		"HKReflect.GameManager" => Instruction.Create(
			OpCodes.Call,
			ModuleDefinition.ImportReference(FindTypeDefinition("GameManager").Methods
				.First(methodDef => methodDef.Name == "get_instance")
			)
		),
		"HKReflect.HeroController" => Instruction.Create(
			OpCodes.Call,
			ModuleDefinition.ImportReference(FindTypeDefinition("HeroController").Methods
				.First(methodDef => methodDef.Name == "get_instance")
			)
		),
		"HKReflect.InputHandler" => Instruction.Create(
			OpCodes.Ldsfld,
			ModuleDefinition.ImportReference(FindTypeDefinition("InputHandler").Fields
				.First(fieldDef => fieldDef.Name == "Instance")
			)
		),
		"HKReflect.ObjectPool" => Instruction.Create(
			OpCodes.Call,
			ModuleDefinition.ImportReference(FindTypeDefinition("ObjectPool").Methods
				.First(methodDef => methodDef.Name == "get_instance")
			)
		),
		"HKReflect.PlayerData" => Instruction.Create(
			OpCodes.Call,
			ModuleDefinition.ImportReference(FindTypeDefinition("PlayerData").Methods
				.First(methodDef => methodDef.Name == "get_instance")
			)
		),
		"HKReflect.SceneData" => Instruction.Create(
			OpCodes.Call,
			ModuleDefinition.ImportReference(FindTypeDefinition("SceneData").Methods
				.First(methodDef => methodDef.Name == "get_instance")
			)
		),
		"HKReflect.UIManager" => Instruction.Create(
			OpCodes.Call,
			ModuleDefinition.ImportReference(FindTypeDefinition("UIManager").Methods
				.First(methodDef => methodDef.Name == "get_instance")
			)
		),
		string name => throw new NotSupportedException("Unsupported singleton type " + name)
	};


	private void Reroute(Instruction[] branchInsts, Instruction from, Instruction to) {
		foreach (Instruction inst in branchInsts) {
			switch (inst.OpCode.OperandType) {
				case OperandType.InlineBrTarget:
				case OperandType.ShortInlineBrTarget:
					Instruction origTarget = inst.Operand as Instruction
						?? throw new InvalidProgramException($"Invalid branching target {inst.Operand}");

					if (origTarget == from) {
						inst.Operand = to;
					}

					break;
				case OperandType.InlineSwitch:
					Instruction[] origTargets = inst.Operand as Instruction[]
						?? throw new InvalidProgramException($"Invalid branching targets {inst.Operand}");

					for (int i = 0; i < origTargets.Length; i++) {
						if (origTargets[i] == from) {
							origTargets[i] = to;
						}
					}

					break;
				default:
					throw new InvalidProgramException($"Invalid branching instruction {inst}");
			}
		}
	}
}
