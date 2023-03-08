# HKReflect

> Compile-time utility that allows easy access to non-public members in Assembly-CSharp with zero runtime overhead

[![NuGet Status](https://img.shields.io/nuget/v/HKReflect.Fody.svg)](https://www.nuget.org/packages/HKReflect.Fody/)
[![Release Version](https://img.shields.io/github/v/release/Clazex/HKReflect?&logo=github&sort=semver)](https://github.com/Clazex/HKReflect/releases/latest)
[![License](https://img.shields.io/github/license/Clazex/HKReflect)](https://github.com/Clazex/HKReflect/blob/main/LICENSE)

This is an add-in for [Fody](https://github.com/Fody/Fody).

## Usage

See also [Fody usage](https://github.com/Fody/Home/blob/master/pages/usage.md).

### Installation

Install [`Fody`](https://www.nuget.org/packages/Fody/) and [`HKReflect.Fody`](https://www.nuget.org/packages/HKReflect.Fody/) as development-only dependencies.

```xml
<!-- In *.csproj -->
<ItemGroup>
	<PackageReference Include="Fody" Version="6.6.4" PrivateAssets="all" />
	<PackageReference Include="HKReflect.Fody" Version="0.2.0" PrivateAssets="all" />
</ItemGroup>
```

### Adding to `FodyWeavers.xml`

Instruct Fody to run HKReflect’s code on the output assembly.

```xml
<Weavers>
	<HKReflect />
</Weavers>
```

### Examples

#### Accessing non-public members

This...

```cs
using HKReflect;

BossSequence sequence = GetBossSequenceFromElsewhere();
// Reflect() extension method from HKReflect.Reflector
LogDebug(sequence.Reflect().bossScenes.Length);

HealthManager hm = GetHealthManagerFromElsewhere();
hm.Reflect().TakeDamage(new HitInstance() { /* ... */ });
```

...compiles to

```cs
BossSequence sequence = GetBossSequenceFromElsewhere();
// Accessing private field bossScenes
LogDebug(sequence.bossScenes.Length);

HealthManager hm = GetHealthManagerFromElsewhere();
// Accessing private method TakeDamage
hm.TakeDamage(new HitInstance() { /* ... */ });
```

#### Accessing non-public members in static classes

This...

```cs
using HKReflect.Static;

LogDebug(BossSequenceControllerR.currentSequence.name);
```

...compiles to

```cs
LogDebug(BossSequenceController.currentSequence.name);
```

#### Accessing instances of singleton classes

This...

```cs
using static HKReflect.Singletons;

// The HeroControllerR property is shorthand for HeroController.instance.Reflect()
HeroControllerR.dashCooldownTimer = 0;
```

...compiles to

```cs
HeroController.instance.dashCooldownTimer = 0;
```

#### Accessing `PlayerData` via `ModHooks`

This...

```cs
using static HKReflect.Singletons;

PlayerDataR.atBench = false;
LogDebug(PlayerDataR.playTime);
LogDebug(PlayerDataR.mapZone);
LogDebug(PlayerDataR.statueStateRadiance);
LogDebug(PlayerDataR.equippedCharms);
```

...compiles to

```cs
PlayerData.instance.SetBoolSwappedArgs(false, "atBench");
LogDebug(PlayerData.instance.GetFloat("playTime"));
LogDebug(PlayerData.instance.GetVariable<MapZone>("mapZone"));
LogDebug(PlayerData.instance.GetVariable<BossStatue.Completion>("statueStateRadiance"));
LogDebug(PlayerData.instance.GetVariable<List<int>>("equippedCharms"));
```

### Restrictions

Due to its nature of only existing in compile-time, some restrictions apply to the usage of HKReflect.

> :boom: - Means this will cause issue at runtime
>
> :construction: - Means this will cause issue at compile-time
>
> :white_check_mark: - Means this will not cause issue

- :boom: Relying on the IL layout of methods that will be processed by HKReflect, i.e., ones that contain references to HKReflect types.
- :boom: Using HKReflect types as generic arguments.
- :boom: Obtaining HKReflect types’ `Type` dynamically, e.g. via `Assembly.GetType`.
- :construction: Declaring fields or properties of HKReflect types.
- :construction: Declaring methods who use HKReflect types as parameters and/or return them.
- :construction: Using `typeof` on HKReflect types.
- :construction: Using HKReflect types as generic constraints.
- :white_check_mark: Declaring local variables of HKReflect types.
- :white_check_mark: Passing instances of HKReflect types to methods that accepts an `object`.​ (Original object will be passed)
- :white_check_mark: Using `GetType` on HKReflect type instances. (Result will be original type)​

### Acquisition of `MemberInfo`s

HKReflect itself does not provide functionality to acquire `MemberInfo`s. It is still needed to use reflection to get them. However, there is another Fody add-in, [InfoOf](https://github.com/Fody/InfoOf), which provides such ability to fetch them at compile-time. See its [Usage](https://github.com/Fody/InfoOf#usage) for details.

When combining HKReflect and InfoOf, it is recommended to run InfoOf before HKReflect.

```xml
<!-- In FodyWeavers.xml -->
<Weavers>
	<InfoOf />
	<HKReflect />
</Weavers>
```

When InfoOf is running before HKReflect, it is safe, though unnecessary, to use it to retrieve HKReflect types’ `MemberInfo`s as HKReflect will replace InfoOf's result to their corresponding original class ones afterwards. However using `nameof` on HKReflect types is still useful. Example:

```cs
FieldInfo fi = Info.OfField<HeroController>(nameof(HKReflect.HeroController.attack_cooldown));
```

The same applies for another Fody add-in, [InlineIL](https://github.com/ltrzesniewski/InlineIL.Fody).

## Mapping rules

### General

- If a type is value type or interface, it is not mapped.
- If a type is an attribute, it is not mapped.
- If a type or a member is not publicly accessible, it is not mapped.
- If a type or method has generic parameters, it is not mapped. (This will only affect few things)
- If a type has no mappable members, it is not mapped.
- If a type is in any of following namespaces, it is not mapped: `System`, `UnityEngine`, `UnityStandardAssets`, `Modding`, `MonoMod`.
- There is a public static class `HKReflect.Reflector` which hosts all `Reflect` methods.
- There is a public static class `HKReflect.Singletons` which hosts all singleton instance accessors.

### Instance classes

- Mapped hierarchically to under `HKReflect` namespace, e.g., `GameManager` goes to `HKReflect.GameManager`, `InControl.Logger` goes to `HKReflect.InControl.Logger`.
- Mapped classes are sealed, and have no constructors.
- If its base class is mapped, then the mapped class got inheritance to the mapped base class. (Yes, inheriting from sealed class is valid in IL)
- Each has a corresponding `Reflect` extension method in type `HKReflect.Reflector` for conversion from original type to mapped type.
- Abstract classes are mapped as sealed but not abstract, because abstract plus sealed equals static in IL.
- Abstract members are not mapped.

### Static classes

- Mapped hierarchically to under `HKReflect.Static` namespace with an `R` suffix added.
- Extension methods are mapped as normal methods.

### Nested classes

- Mapped to be a nested class under mapped version of its declaring class.
- Instance classes rules or static classes rules are applied accordingly, except the namespace rule.

### Singleton classes

- These classes are considered as singleton classes: `GameCameras`, `GameManager`, `HeroController`, `InputHandler`, `ObjectPool`, `PlayerData`, `SceneData`, `UIManager`.
- Each has a corresponding getter-only property named after the type itself along with an `R` suffix in type `HKReflect.Singletons`, which functions as a shorthand for getting its instance and then call `Reflect`.

## Architecture

- `HKReflect`: Bundles the result of other 2 projects into a single nuget package.
- `HKReflect.Fody`: Processes the assembly with `Mono.Cecil` to replace all `HKReflect` references to their corresponding original types. Executes when the consumer project is compiling.
- `ReflectGen`: Reads `Assembly-CSharp` and uses `Mono.Cecil` to generate an assembly with the publicized and mapped versions of types in it, as well as utility classes, for reference. Executes when building this package, does not exist in the package itself.

## Behind the scenes

There are mainly three things that stop one from accessing non-public members: auto-completion, compiler and runtime. Auto-completion will not include non-public members, compiler will fail to compile and runtime will throw exceptions. HKReflect bypasses the first two by generating a new assembly resembling the original `Assembly-CSharp` but without actual definitions (all methods are `extern`). But it tackles with the last one differently.

When JIT compiling a method for the first time, Mono runtime performs a process called visibility verification to check if the method can actually access all the members it referenced, and, if not, throws `FieldAccessException` or `MethodAccessException`. It prevents the case when an assembly has changed something from public to private, but a not yet recompiled assembly that depends on it can still access that thing. HKReflect (as well as [MonoMod](https://github.com/MonoMod/MonoMod)), however, attaches a special attribute to the assembly to instruct Mono runtime to skip this procedure for all methods within this assembly, and thus makes it feasible to access non-public members at runtime.

Therefore, HKReflect has a side-effect to allow processed assembly to be able to access a member which is public in compile-time version, and non-public in runtime version, in dependencies other than `Assembly-CSharp`.

