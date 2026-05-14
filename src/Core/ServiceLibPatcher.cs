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

        public bool AccountLimitBypassPatched { get; set; }

        public bool AddAccountDeepLinkPatched { get; set; }

        public bool AlreadyPatched
        {
            get
            {
                return !AvailableSettingsPatched
                    && !LaunchSettingsPatched
                    && !MonitorDisplayPatched
                    && !RuntimeDisplaySettingsPatched
                    && !SharpeningFilterPatched
                    && !AccountLimitBypassPatched
                    && !AddAccountDeepLinkPatched;
            }
        }
    }

    internal static class ServiceLibPatcher
    {
        public sealed class AccountLimitBypassMethods
        {
            public MethodDef LocalStateUpdateAccountsInfo { get; set; }

            public MethodDef AccountsInfoUpdaterMoveNext { get; set; }

            public MethodDef GlobalStateSetOnboardedAccounts { get; set; }
        }

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
                var accountLimitMethods = FindAccountLimitBypassMethods(module);
                var openDeepLinkMethod = FindOpenDeepLinkMethod(module);

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
                    AccountLimitBypassPatched = PatchAccountLimitBypass(module, accountLimitMethods),
                    AddAccountDeepLinkPatched = PatchAddAccountDeepLink(module, openDeepLinkMethod),
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

        public static AccountLimitBypassMethods FindAccountLimitBypassMethods(ModuleDef module)
        {
            return new AccountLimitBypassMethods
            {
                LocalStateUpdateAccountsInfo = FindLocalStateUpdateAccountsInfoMethod(module),
                AccountsInfoUpdaterMoveNext = FindMethod(
                    module,
                    GpgConstants.AccountsInfoUpdaterMoveNextTypeName,
                    GpgConstants.MoveNextMethodName,
                    "System.Void",
                    new string[0]),
                GlobalStateSetOnboardedAccounts = FindMethod(
                    module,
                    GpgConstants.GlobalStateAccountCountMutatorTypeName,
                    GpgConstants.GlobalStateSetOnboardedAccountsMethodName,
                    "System.Boolean",
                    new[] { "Google.Play.Games.Metrics.GlobalState" }),
            };
        }

        public static bool HasAccountLimitBypass(AccountLimitBypassMethods methods)
        {
            return methods != null
                && HasHookCall(methods.LocalStateUpdateAccountsInfo, GpgConstants.PatchOnboardedAccountCountMethod)
                && HasHookCall(methods.AccountsInfoUpdaterMoveNext, GpgConstants.PatchOnboardedAccountCountMethod)
                && HasHookCall(methods.GlobalStateSetOnboardedAccounts, GpgConstants.PatchOnboardedAccountCountMethod);
        }

        public static MethodDef FindOpenDeepLinkMethod(ModuleDef module)
        {
            return FindMethod(
                module,
                GpgConstants.ClientControllerTypeName,
                GpgConstants.OpenDeepLinkMethodName,
                "System.Threading.Tasks.Task",
                new[] { "System.String" });
        }

        public static bool HasAddAccountDeepLink(MethodDef method)
        {
            if (method == null || method.Body == null)
            {
                return false;
            }

            var sawProtocol = false;
            var sawAddAccountCall = false;
            foreach (var instruction in method.Body.Instructions)
            {
                if (instruction.OpCode == OpCodes.Ldstr
                    && string.Equals(instruction.Operand as string, GpgConstants.AddAccountProtocolUrl, StringComparison.Ordinal))
                {
                    sawProtocol = true;
                    continue;
                }

                var methodOperand = instruction.Operand as IMethod;
                if ((instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt)
                    && methodOperand != null
                    && string.Equals(methodOperand.Name, GpgConstants.AddAccountMethodName, StringComparison.Ordinal))
                {
                    sawAddAccountCall = true;
                }
            }

            return sawProtocol && sawAddAccountCall;
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

        private static bool PatchAccountLimitBypass(ModuleDef module, AccountLimitBypassMethods methods)
        {
            var changed = false;
            changed |= PatchOnboardedAccountCountSetter(
                module,
                methods.LocalStateUpdateAccountsInfo,
                GpgConstants.AccountsInfoTypeName,
                "set_OnboardedAccounts",
                "set_NumOnboardedAccounts");
            changed |= PatchOnboardedAccountCountSetter(
                module,
                methods.AccountsInfoUpdaterMoveNext,
                GpgConstants.AccountsInfoTypeName,
                "set_OnboardedAccounts",
                "set_NumOnboardedAccounts");
            changed |= PatchOnboardedAccountCountSetter(
                module,
                methods.GlobalStateSetOnboardedAccounts,
                GpgConstants.GlobalStateTypeName,
                "set_OnboardedAccounts");

            return changed;
        }

        private static bool PatchAddAccountDeepLink(ModuleDef module, MethodDef openDeepLinkMethod)
        {
            if (HasAddAccountDeepLink(openDeepLinkMethod))
            {
                return false;
            }

            var addAccountMethod = FindMethod(
                module,
                GpgConstants.ClientControllerTypeName,
                GpgConstants.AddAccountMethodName,
                "System.Threading.Tasks.Task",
                new string[0]);

            var body = openDeepLinkMethod.Body;
            var firstOriginal = body.Instructions.First();
            var stringEquals = new MemberRefUser(
                module,
                "op_Equality",
                MethodSig.CreateStatic(
                    module.CorLibTypes.Boolean,
                    module.CorLibTypes.String,
                    module.CorLibTypes.String),
                module.CorLibTypes.String.ToTypeDefOrRef());

            body.Instructions.Insert(0, Instruction.Create(OpCodes.Ldarg_1));
            body.Instructions.Insert(1, Instruction.Create(OpCodes.Ldstr, GpgConstants.AddAccountProtocolUrl));
            body.Instructions.Insert(2, Instruction.Create(OpCodes.Call, stringEquals));
            body.Instructions.Insert(3, Instruction.Create(OpCodes.Brfalse_S, firstOriginal));
            body.Instructions.Insert(4, Instruction.Create(OpCodes.Ldarg_0));
            body.Instructions.Insert(5, Instruction.Create(OpCodes.Call, addAccountMethod));
            body.Instructions.Insert(6, Instruction.Create(OpCodes.Ret));
            body.MaxStack = (ushort)Math.Max((int)body.MaxStack, 2);
            return true;
        }

        private static bool PatchOnboardedAccountCountSetter(
            ModuleDef module,
            MethodDef method,
            string declaringTypeName,
            params string[] setterNames)
        {
            if (HasHookCall(method, GpgConstants.PatchOnboardedAccountCountMethod))
            {
                return false;
            }

            var hookMethod = CreateHookMethodReference(
                module,
                GpgConstants.PatchOnboardedAccountCountMethod,
                module.CorLibTypes.Int32,
                module.CorLibTypes.Int32);

            var body = method.Body;
            for (var index = 0; index < body.Instructions.Count; index++)
            {
                var instruction = body.Instructions[index];
                var calledMethod = instruction.Operand as IMethod;
                if ((instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt)
                    && calledMethod != null
                    && setterNames.Any(setterName => string.Equals(calledMethod.Name, setterName, StringComparison.Ordinal))
                    && calledMethod.DeclaringType != null
                    && string.Equals(calledMethod.DeclaringType.FullName, declaringTypeName, StringComparison.Ordinal))
                {
                    body.Instructions.Insert(index, Instruction.Create(OpCodes.Call, hookMethod));
                    body.MaxStack = (ushort)Math.Max((int)body.MaxStack, 8);
                    return true;
                }
            }

            throw new FriendlyException(
                "Could not find " + declaringTypeName + " account-count setter call in method '" + method.FullName + "'.");
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

        private static MethodDef FindLocalStateUpdateAccountsInfoMethod(ModuleDef module)
        {
            try
            {
                return FindMethod(
                    module,
                    GpgConstants.LocalStateModuleTypeName,
                    GpgConstants.UpdateAccountsInfoMethodName,
                    "Google.Hpe.Client.V1.AccountsInfo",
                    new string[0]);
            }
            catch (FriendlyException oldShapeException)
            {
                try
                {
                    return FindMethod(
                        module,
                        GpgConstants.LocalStateModuleTypeName,
                        GpgConstants.UpdateAccountsInfoMethodName,
                        "System.Boolean",
                        new[]
                        {
                            "Google.Hpe.Client.V1.LocalState",
                            "Google.Hpe.Service.Accounts.AccountsState",
                        });
                }
                catch (FriendlyException newShapeException)
                {
                    throw new FriendlyException(
                        oldShapeException.Message + " " + newShapeException.Message);
                }
            }
        }

        private static MethodDef FindMethod(
            ModuleDef module,
            string typeName,
            string methodName,
            string returnTypeName,
            IReadOnlyList<string> parameterTypeNames)
        {
            var type = module.GetTypes().FirstOrDefault(candidate =>
                string.Equals(candidate.FullName, typeName, StringComparison.Ordinal));

            if (type == null)
            {
                throw new FriendlyException("Could not find " + typeName + " in ServiceLib.dll.");
            }

            var method = type.Methods.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, methodName, StringComparison.Ordinal)
                && candidate.MethodSig != null
                && candidate.ReturnType.FullName == returnTypeName
                && candidate.MethodSig.Params.Count == parameterTypeNames.Count
                && candidate.MethodSig.Params
                    .Select(parameter => parameter.FullName)
                    .SequenceEqual(parameterTypeNames));

            if (method == null)
            {
                throw new FriendlyException("Could not find target method '" + methodName + "' in " + typeName + ".");
            }

            if (method.Body == null)
            {
                throw new FriendlyException("Target method '" + methodName + "' has no IL body.");
            }

            return method;
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
