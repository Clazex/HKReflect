using Fody;

using Mono.Cecil;

namespace HKReflect.Fody;

public sealed partial class ModuleWeaver {
	private void ProcessField(FieldDefinition fieldDef) {
		if (fieldDef.FieldType.IsHKReflectType()) {
			throw new WeavingException(fieldDef.FullName + " contains reflected type, please use original type instead");
		}
	}
}
