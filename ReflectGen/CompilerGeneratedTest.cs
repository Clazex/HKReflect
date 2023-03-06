using System.Linq;
using System.Runtime.CompilerServices;

using Mono.Cecil;

namespace ReflectGen;

internal sealed partial class Program {
	private static bool IsCompilerGenerated(ICustomAttributeProvider provider) =>
		provider.CustomAttributes.Any(IsCompilerGeneratedAttribute);

	private static bool IsCompilerGeneratedAttribute(CustomAttribute attr) =>
		attr.AttributeType.FullName == typeof(CompilerGeneratedAttribute).FullName;
}
