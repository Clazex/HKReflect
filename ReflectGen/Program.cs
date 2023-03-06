using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

using Mono.Cecil;

namespace ReflectGen;

internal sealed partial class Program {
	private static readonly string[] namespacesToSkip = new[] {
		"System",
		"UnityEngine",
		"UnityStandardAssets",
		"Modding",
		"MonoMod"
	};

	private static readonly string[] singletonClasses = new[] {
		"GameCameras",
		"GameManager",
		"HeroController",
		"InputHandler",
		"ObjectPool",
		"PlayerData",
		"SceneData",
		"UIManager"
	};

	private static void Main(string[] args) {
		if (args.Length != 3) {
			throw new ArgumentException($"Usage: ReflectGen <ASSEMBLY-CSHARP> <BASE NAMESPACE> <OUT FILE>");
		}

		string asmPath = args[0];
		string baseNs = args[1];
		string outFile = args[2];


		DefaultAssemblyResolver asmResolver = new();
		asmResolver.AddSearchDirectory(Path.GetDirectoryName(asmPath));

		FileStream asmFile = File.OpenRead(asmPath);
		using var origAsm = AssemblyDefinition.ReadAssembly(asmFile, new() {
			AssemblyResolver = asmResolver
		});
		asmFile.Dispose();

		using var resAsm = AssemblyDefinition.CreateAssembly(
			new(baseNs, origAsm.Name.Version),
			baseNs + ".dll",
			new ModuleParameters() {
				Kind = ModuleKind.Dll,
				Runtime = TargetRuntime.Net_4_0,
				Timestamp = 0
			}
		);
		ModuleDefinition module = resAsm.MainModule;

		TypeDefinition reflectorType = new(
			baseNs,
			"Reflector",
			TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit
		);
		reflectorType.CustomAttributes.Add(new(module.ImportReference(typeof(ExtensionAttribute).GetConstructor(Type.EmptyTypes))));
		module.Types.Add(reflectorType);

		TypeDefinition singletonsType = new(
			baseNs,
			"Singletons",
			TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit
		);
		module.Types.Add(singletonsType);

		Dictionary<TypeDefinition, string> typesToAddInheritance = new();


		foreach (TypeDefinition typeDef in origAsm.MainModule.Types) {
			if (namespacesToSkip.Any(ns => typeDef.Namespace == ns || typeDef.Namespace.StartsWith(ns + '.'))) {
				continue;
			}

			if (GenerateClass(typeDef, module, baseNs, reflectorType, typesToAddInheritance) is TypeDefinition mappedType) {
				module.Types.Add(mappedType);

				if (singletonClasses.Contains(typeDef.FullName)) {
					GenerateSingletonAccess(mappedType, singletonsType);
				}
			}
		}

		foreach ((TypeDefinition derived, string baseName) in typesToAddInheritance) {
			if (module.GetType(baseName) is TypeDefinition baseType) {
				derived.BaseType = baseType;
			}
		}

		resAsm.Write(outFile, new() {
			DeterministicMvid = true
		});
	}
}
