using System;
using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;

namespace GpgPatcher
{
    internal sealed class ServiceLibPatchResult
    {
        public bool AvailableSettingsPatched { get; set; }

        public bool LaunchSettingsPatched { get; set; }

        public bool MonitorDisplayPatched { get; set; }

        public bool RuntimeDisplaySettingsPatched { get; set; }

        public bool SharpeningFilterPatched { get; set; }

        public bool AlreadyPatched
        {
            get
            {
                return !AvailableSettingsPatched
                    && !LaunchSettingsPatched
                    && !MonitorDisplayPatched
                    && !RuntimeDisplaySettingsPatched
                    && !SharpeningFilterPatched;
            }
        }
    }

    internal static class ServiceLibPatcher
    {
        public static ServiceLibPatchResult Patch(string serviceLibPath, string outputPath)
        {
            using (var module = ModuleDefMD.Load(serviceLibPath))
            {
                var serviceType = module.Types.FirstOrDefault(type => type.FullName == GpgConstants.ServiceTypeName);
                if (serviceType == null)
                {
                    throw new FriendlyException("Could not find AppSessionScope in ServiceLib.dll.");
                }

                var availableMethod = FindTargetMethod(serviceType, GpgConstants.AvailableSettingsMethodName);
                var launchMethod = FindTargetMethod(serviceType, GpgConstants.LaunchSettingsMethodName);
                var monitorDisplayMethod = FindMonitorDisplayMethod(serviceType, GpgConstants.MonitorDisplayMethodName);
                var runtimeDisplayMethod = FindRuntimeDisplaySettingsMethod(serviceType, GpgConstants.RuntimeDisplaySettingsMethodName);
                var sharpeningGetter = FindSharpeningFilterGetter(module);
                var sharpeningRequestMethod = FindSharpeningFilterRequestMethod(module);

                var result = new ServiceLibPatchResult
                {
                    AvailableSettingsPatched = PatchMethod(module, availableMethod, GpgConstants.PatchAvailableSettingsMethod),
                    LaunchSettingsPatched = PatchMethod(module, launchMethod, GpgConstants.PatchAndroidDisplaySettingsMethod),
                    MonitorDisplayPatched = PatchReturnWithLaunchRequestField(
                        module,
                        serviceType,
                        monitorDisplayMethod,
                        GpgConstants.PatchMonitorDisplaySizeMethod),
                    RuntimeDisplaySettingsPatched = PatchFirstParameterWithLaunchRequestField(
                        module,
                        serviceType,
                        runtimeDisplayMethod,
                        GpgConstants.PatchRuntimeAndroidDisplaySettingsMethod),
                    SharpeningFilterPatched = PatchSharpeningFilter(
                        module,
                        sharpeningGetter,
                        sharpeningRequestMethod),
                };

                var options = new ModuleWriterOptions(module)
                {
                    Logger = DummyLogger.NoThrowInstance,
                };

                module.Write(outputPath, options);
                return result;
            }
        }

        public static MethodDef FindTargetMethod(TypeDef type, string name)
        {
            var method = type.Methods.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, name, StringComparison.Ordinal)
                && !candidate.IsStatic
                && candidate.MethodSig != null
                && candidate.MethodSig.Params.Count == 2
                && candidate.MethodSig.Params[0].FullName == "Google.Hpe.Service.V1.DisplaySize"
                && candidate.MethodSig.Params[1].FullName == "Google.Hpe.Service.V1.LaunchGameRequest");

            if (method == null)
            {
                throw new FriendlyException("Could not find target method '" + name + "' in AppSessionScope.");
            }

            if (method.Body == null)
            {
                throw new FriendlyException("Target method '" + name + "' has no IL body.");
            }

            return method;
        }

        public static MethodDef FindMonitorDisplayMethod(TypeDef type, string name)
        {
            var method = type.Methods.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, name, StringComparison.Ordinal)
                && !candidate.IsStatic
                && candidate.MethodSig != null
                && candidate.MethodSig.Params.Count == 1
                && candidate.MethodSig.Params[0].FullName == "System.Int64"
                && candidate.ReturnType.FullName == "Google.Hpe.Service.V1.DisplaySize");

            if (method == null)
            {
                throw new FriendlyException("Could not find target method '" + name + "' in AppSessionScope.");
            }

            if (method.Body == null)
            {
                throw new FriendlyException("Target method '" + name + "' has no IL body.");
            }

            return method;
        }

        public static MethodDef FindRuntimeDisplaySettingsMethod(TypeDef type, string name)
        {
            var method = type.Methods.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, name, StringComparison.Ordinal)
                && !candidate.IsStatic
                && candidate.MethodSig != null
                && candidate.MethodSig.Params.Count == 1
                && candidate.MethodSig.Params[0].FullName == "Google.Hpe.Service.V1.AndroidDisplaySettings"
                && candidate.ReturnType.FullName == "System.Threading.Tasks.Task");

            if (method == null)
            {
                throw new FriendlyException("Could not find target method '" + name + "' in AppSessionScope.");
            }

            if (method.Body == null)
            {
                throw new FriendlyException("Target method '" + name + "' has no IL body.");
            }

            return method;
        }

        public static MethodDef FindSharpeningFilterGetter(ModuleDef module)
        {
            var type = module.Types.FirstOrDefault(candidate =>
                string.Equals(candidate.FullName, GpgConstants.KiwiEmulatorConfigurationTypeName, StringComparison.Ordinal));

            if (type == null)
            {
                throw new FriendlyException("Could not find KiwiEmulatorConfiguration in ServiceLib.dll.");
            }

            var method = type.Methods.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, GpgConstants.EnableSharpeningFilterGetterName, StringComparison.Ordinal)
                && !candidate.IsStatic
                && candidate.MethodSig != null
                && candidate.MethodSig.Params.Count == 0
                && candidate.ReturnType.FullName == "System.Boolean");

            if (method == null)
            {
                throw new FriendlyException("Could not find EnableSharpeningFilter getter in KiwiEmulatorConfiguration.");
            }

            if (method.Body == null)
            {
                throw new FriendlyException("EnableSharpeningFilter getter has no IL body.");
            }

            return method;
        }

        public static MethodDef FindSharpeningFilterRequestMethod(ModuleDef module)
        {
            var type = module.Types.FirstOrDefault(candidate =>
                string.Equals(candidate.FullName, GpgConstants.EmulatorReadyControllerTypeName, StringComparison.Ordinal));

            if (type == null)
            {
                throw new FriendlyException("Could not find EmulatorReadyController in ServiceLib.dll.");
            }

            var method = type.Methods.FirstOrDefault(candidate =>
                candidate.Name.EndsWith(GpgConstants.EnableSharpeningFilterRequestMethodName, StringComparison.Ordinal)
                && !candidate.IsStatic
                && candidate.MethodSig != null
                && candidate.MethodSig.Params.Count == 1
                && candidate.MethodSig.Params[0].FullName == GpgConstants.SharpeningFilterRequestTypeName
                && candidate.ReturnType.FullName == "System.Threading.Tasks.Task`1<System.Boolean>");

            if (method == null)
            {
                throw new FriendlyException("Could not find EnableSharpeningFilterAsync in EmulatorReadyController.");
            }

            if (method.Body == null)
            {
                throw new FriendlyException("EnableSharpeningFilterAsync has no IL body.");
            }

            return method;
        }

        public static bool HasAnyHookCall(MethodDef method, string hookMethodName)
        {
            return HasHookCall(method, hookMethodName)
                || HasLegacyHookCall(method, hookMethodName);
        }

        public static bool HasHookCall(MethodDef method, string hookMethodName)
        {
            return HasHookCall(method, hookMethodName, GpgConstants.HookTypeNamespace);
        }

        public static bool HasLegacyHookCall(MethodDef method, string hookMethodName)
        {
            return HasHookCall(method, hookMethodName, GpgConstants.LegacyHookTypeNamespace);
        }

        private static bool HasHookCall(MethodDef method, string hookMethodName, string hookTypeNamespace)
        {
            if (method == null || method.Body == null)
            {
                return false;
            }

            foreach (var instruction in method.Body.Instructions)
            {
                if (instruction.OpCode != OpCodes.Call)
                {
                    continue;
                }

                var operand = instruction.Operand as IMethod;
                if (operand == null)
                {
                    continue;
                }

                if (string.Equals(operand.Name, hookMethodName, StringComparison.Ordinal)
                    && operand.DeclaringType != null
                    && string.Equals(operand.DeclaringType.ReflectionNamespace, hookTypeNamespace, StringComparison.Ordinal)
                    && string.Equals(operand.DeclaringType.Name, GpgConstants.HookTypeName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsConstantTrueMethod(MethodDef method)
        {
            if (method == null || method.Body == null)
            {
                return false;
            }

            var meaningful = method.Body.Instructions
                .Where(instruction => instruction.OpCode != OpCodes.Nop)
                .ToList();

            return meaningful.Count == 2
                && meaningful[0].OpCode == OpCodes.Ldc_I4_1
                && meaningful[1].OpCode == OpCodes.Ret;
        }

        public static bool HasForcedSharpeningFilterRequest(MethodDef method)
        {
            if (method == null || method.Body == null || method.Body.Instructions.Count < 3)
            {
                return false;
            }

            var instructions = method.Body.Instructions;
            if (instructions[0].OpCode != OpCodes.Ldarg_1 || instructions[1].OpCode != OpCodes.Ldc_I4_1)
            {
                return false;
            }

            var operand = instructions[2].Operand as IMethod;
            return instructions[2].OpCode == OpCodes.Callvirt
                && operand != null
                && string.Equals(operand.Name, "set_Enabled", StringComparison.Ordinal)
                && operand.DeclaringType != null
                && string.Equals(operand.DeclaringType.FullName, GpgConstants.SharpeningFilterRequestTypeName, StringComparison.Ordinal);
        }

        private static bool PatchMethod(ModuleDef module, MethodDef method, string hookMethodName)
        {
            if (HasHookCall(method, hookMethodName))
            {
                return false;
            }

            if (HasLegacyHookCall(method, hookMethodName))
            {
                throw new FriendlyException(
                    "A legacy pre-rename patch was detected. Restore the original files first, then apply the current GPG Patcher build.");
            }

            if (method.IsStatic || method.MethodSig.Params.Count != 2)
            {
                throw new FriendlyException("Target method signature changed for '" + method.Name + "'.");
            }

            var hookMethod = CreateHookMethodReference(module, method, hookMethodName);
            var body = method.Body;
            var resultLocal = new Local(method.ReturnType);
            body.Variables.Add(resultLocal);
            body.InitLocals = true;

            var returns = new List<Instruction>();
            foreach (var instruction in body.Instructions)
            {
                if (instruction.OpCode == OpCodes.Ret)
                {
                    returns.Add(instruction);
                }
            }

            if (returns.Count == 0)
            {
                throw new FriendlyException("Target method '" + method.Name + "' had no return instructions.");
            }

            foreach (var ret in returns)
            {
                var index = body.Instructions.IndexOf(ret);
                ret.OpCode = OpCodes.Stloc;
                ret.Operand = resultLocal;
                body.Instructions.Insert(index + 1, Instruction.Create(OpCodes.Ldloc, resultLocal));
                body.Instructions.Insert(index + 2, Instruction.Create(OpCodes.Ldarg_2));
                body.Instructions.Insert(index + 3, Instruction.Create(OpCodes.Call, hookMethod));
                body.Instructions.Insert(index + 4, Instruction.Create(OpCodes.Ret));
            }

            body.MaxStack = (ushort)Math.Max((int)body.MaxStack, 8);
            return true;
        }

        private static bool PatchReturnWithLaunchRequestField(
            ModuleDef module,
            TypeDef serviceType,
            MethodDef method,
            string hookMethodName)
        {
            if (HasHookCall(method, hookMethodName))
            {
                return false;
            }

            var launchGameRequestField = FindLaunchGameRequestField(serviceType);
            var hookMethod = CreateHookMethodReference(
                module,
                hookMethodName,
                method.ReturnType,
                method.ReturnType,
                launchGameRequestField.FieldType);

            var body = method.Body;
            var resultLocal = new Local(method.ReturnType);
            body.Variables.Add(resultLocal);
            body.InitLocals = true;

            var returns = new List<Instruction>();
            foreach (var instruction in body.Instructions)
            {
                if (instruction.OpCode == OpCodes.Ret)
                {
                    returns.Add(instruction);
                }
            }

            if (returns.Count == 0)
            {
                throw new FriendlyException("Target method '" + method.Name + "' had no return instructions.");
            }

            foreach (var ret in returns)
            {
                var index = body.Instructions.IndexOf(ret);
                ret.OpCode = OpCodes.Stloc;
                ret.Operand = resultLocal;
                body.Instructions.Insert(index + 1, Instruction.Create(OpCodes.Ldloc, resultLocal));
                body.Instructions.Insert(index + 2, Instruction.Create(OpCodes.Ldarg_0));
                body.Instructions.Insert(index + 3, Instruction.Create(OpCodes.Ldfld, launchGameRequestField));
                body.Instructions.Insert(index + 4, Instruction.Create(OpCodes.Call, hookMethod));
                body.Instructions.Insert(index + 5, Instruction.Create(OpCodes.Ret));
            }

            body.MaxStack = (ushort)Math.Max((int)body.MaxStack, 8);
            return true;
        }

        private static bool PatchFirstParameterWithLaunchRequestField(
            ModuleDef module,
            TypeDef serviceType,
            MethodDef method,
            string hookMethodName)
        {
            if (HasHookCall(method, hookMethodName))
            {
                return false;
            }

            if (!method.HasThis || method.Parameters.Count < 2)
            {
                throw new FriendlyException("Target method '" + method.Name + "' does not have the expected instance parameter layout.");
            }

            var launchGameRequestField = FindLaunchGameRequestField(serviceType);
            var parameterType = method.MethodSig.Params[0];
            var hookMethod = CreateHookMethodReference(
                module,
                hookMethodName,
                parameterType,
                parameterType,
                launchGameRequestField.FieldType);

            var body = method.Body;
            var first = body.Instructions[0];
            body.Instructions.Insert(0, Instruction.Create(OpCodes.Ldarg_1));
            body.Instructions.Insert(1, Instruction.Create(OpCodes.Ldarg_0));
            body.Instructions.Insert(2, Instruction.Create(OpCodes.Ldfld, launchGameRequestField));
            body.Instructions.Insert(3, Instruction.Create(OpCodes.Call, hookMethod));
            body.Instructions.Insert(4, Instruction.Create(OpCodes.Starg_S, method.Parameters[1]));

            body.MaxStack = (ushort)Math.Max((int)body.MaxStack, 8);
            return first != body.Instructions[0];
        }

        private static bool PatchSharpeningFilter(
            ModuleDef module,
            MethodDef sharpeningGetter,
            MethodDef sharpeningRequestMethod)
        {
            var changed = false;

            if (!IsConstantTrueMethod(sharpeningGetter))
            {
                var body = sharpeningGetter.Body;
                body.ExceptionHandlers.Clear();
                body.Variables.Clear();
                body.Instructions.Clear();
                body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I4_1));
                body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                body.InitLocals = false;
                body.MaxStack = 1;
                changed = true;
            }

            if (!HasForcedSharpeningFilterRequest(sharpeningRequestMethod))
            {
                var body = sharpeningRequestMethod.Body;
                var setter = CreateSharpeningFilterEnabledSetterReference(
                    module,
                    sharpeningRequestMethod.MethodSig.Params[0]);

                body.Instructions.Insert(0, Instruction.Create(OpCodes.Ldarg_1));
                body.Instructions.Insert(1, Instruction.Create(OpCodes.Ldc_I4_1));
                body.Instructions.Insert(2, Instruction.Create(OpCodes.Callvirt, setter));
                body.MaxStack = (ushort)Math.Max((int)body.MaxStack, 2);
                changed = true;
            }

            return changed;
        }

        private static FieldDef FindLaunchGameRequestField(TypeDef serviceType)
        {
            var field = serviceType.Fields.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, GpgConstants.LaunchGameRequestFieldName, StringComparison.Ordinal)
                && candidate.FieldType.FullName == "Google.Hpe.Service.V1.LaunchGameRequest");

            if (field == null)
            {
                throw new FriendlyException("Could not find AppSessionScope launch request field.");
            }

            return field;
        }

        private static MemberRef CreateHookMethodReference(ModuleDef module, MethodDef targetMethod, string hookMethodName)
        {
            return CreateHookMethodReference(
                module,
                hookMethodName,
                targetMethod.ReturnType,
                targetMethod.ReturnType,
                targetMethod.MethodSig.Params[1]);
        }

        private static MemberRef CreateHookMethodReference(
            ModuleDef module,
            string hookMethodName,
            TypeSig returnType,
            params TypeSig[] parameterTypes)
        {
            var assemblyRef = module.GetAssemblyRefs()
                .FirstOrDefault(reference => string.Equals(reference.Name, GpgConstants.HookAssemblyName, StringComparison.Ordinal));

            if (assemblyRef == null)
            {
                var assemblyName = new AssemblyNameInfo(GpgConstants.HookAssemblyName);
                assemblyRef = new AssemblyRefUser(assemblyName);
            }

            var hookType = new TypeRefUser(
                module,
                GpgConstants.HookTypeNamespace,
                GpgConstants.HookTypeName,
                assemblyRef);

            var signature = MethodSig.CreateStatic(returnType, parameterTypes);

            return new MemberRefUser(module, hookMethodName, signature, hookType);
        }

        private static MemberRef CreateSharpeningFilterEnabledSetterReference(
            ModuleDef module,
            TypeSig requestType)
        {
            var signature = MethodSig.CreateInstance(module.CorLibTypes.Void, module.CorLibTypes.Boolean);
            return new MemberRefUser(module, "set_Enabled", signature, requestType.ToTypeDefOrRef());
        }
    }
}
