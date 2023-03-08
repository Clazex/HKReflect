using System;
using System.Collections.Generic;
using System.Linq;

using Mono.Cecil;

namespace HKReflect.Fody;

internal static class Extensions {
	public static string StripStart(this string self, string val) =>
		self.StartsWith(val) ? self.Substring(val.Length) : self;


	internal static void ForEach<T>(this IEnumerable<T> self, Action<T> action) {
		foreach (T i in self) {
			action.Invoke(i);
		}
	}

	internal static void ParallelForEach<T>(this IEnumerable<T> self, Action<T> action) => self
		.AsParallel()
		.WithExecutionMode(ParallelExecutionMode.ForceParallelism)
		.AsUnordered()
		.ForEach(action);


	internal static bool IsInNamespace(this TypeReference self, string ns) =>
		self.DeclaringType?.IsInNamespace(ns) ?? (self.Namespace == ns || self.Namespace.StartsWith(ns + '.'));

	internal static bool IsHKReflectType(this TypeReference self) => self.IsInNamespace(nameof(HKReflect));


	internal static GenericInstanceMethod MakeGenericMethod(this MethodReference self, params TypeReference[] arguments) {
		if (arguments.Length != self.GenericParameters.Count) {
			throw new ArgumentException(
				nameof(arguments),
				$"Generic argument count mismatch, expects {self.GenericParameters.Count}, got {arguments.Length}"
			);
		}

		GenericInstanceMethod genericMethod = new(self);

		foreach (TypeReference type in arguments) {
			genericMethod.GenericArguments.Add(type);
		}

		return genericMethod;
	}
}
