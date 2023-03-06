using System.Linq;
using System.Security.Permissions;

using Mono.Cecil;

using SecurityAction = Mono.Cecil.SecurityAction;

namespace HKReflect.Fody;

public sealed partial class ModuleWeaver {
	/// <summary>
	/// Check whether the module's assembly has been set to bypass runtime
	/// visibility verification in Mono.
	/// </summary>
	/// <param name="module">Module to check bypass</param>
	/// <returns>If the verification is bypassed</returns>
	private static bool GetSkippedVisibilityCheck(ModuleDefinition module) =>
		module.Assembly.SecurityDeclarations
			.Where(decl => decl.Action == SecurityAction.RequestMinimum)
			.SelectMany(decl => decl.SecurityAttributes)
			.Cast<ICustomAttribute>()
			.Concat(module.Assembly.CustomAttributes)
			.Any(attr =>
				attr.AttributeType.FullName == typeof(SecurityPermissionAttribute).FullName
					&& attr.Properties.Any(prop =>
						prop.Name == nameof(SecurityPermissionAttribute.Action)
						&& prop.Argument.Type.FullName == typeof(SecurityAction).FullName
						&& prop.Argument.Value is SecurityAction.RequestMinimum
					)
					&& attr.Properties.Any(prop =>
						prop.Name == nameof(SecurityPermissionFlag.SkipVerification)
						&& prop.Argument.Type == module.TypeSystem.Boolean
						&& prop.Argument.Value is true
					)
			);

	/// <summary>
	/// Allow the module's assembly to bypass runtime visibility
	/// verification in Mono.
	/// </summary>
	/// <param name="module">Module to set bypass</param>
	private static void SetSkippedVisibilityCheck(ModuleDefinition module) =>
		module.Assembly.SecurityDeclarations.Add(new(SecurityAction.RequestMinimum) {
			SecurityAttributes = {
				new(module.ImportReference(typeof(SecurityPermissionAttribute))) {
					Properties = {
						new(
							nameof(SecurityPermissionFlag.SkipVerification),
							new(module.TypeSystem.Boolean, true)
						)
					}
				}
			}
		});

	/// <summary>
	/// Ensure the module's assembly has been set to bypass runtime
	/// visibility verification in Mono.
	/// </summary>
	/// <param name="module">Module to ensure bypass</param>
	private static void EnsureSkippedVisibilityCheck(ModuleDefinition module) {
		if (GetSkippedVisibilityCheck(module)) {
			return;
		}

		SetSkippedVisibilityCheck(module);
	}
}
