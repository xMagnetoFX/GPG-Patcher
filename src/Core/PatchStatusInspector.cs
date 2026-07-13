using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using dnlib.DotNet;

namespace GpgPatcher
{
    internal static class PatchStatusInspector
    {
        public static string GetInstalledVersion(PlayGamesInstallLayout layout)
        {
            layout.EnsureInstallationExists();
            return GetInstalledVersion(layout.ServiceExePath);
        }

        public static string GetInstalledVersion(string serviceExePath)
        {
            var version = System.Diagnostics.FileVersionInfo.GetVersionInfo(serviceExePath).FileVersion;
            return string.IsNullOrWhiteSpace(version) ? string.Empty : version.Trim();
        }

        public static PatchStatus Inspect(PlayGamesInstallLayout layout)
        {
            layout.EnsureInstallationExists();

            var patchStatus = new PatchStatus
            {
                InstalledVersion = GetInstalledVersion(layout.ServiceExePath),
                TargetResolution = ResolutionProfileStorage.ReadApplied(layout),
                HookDllPresent = File.Exists(layout.HookTargetPath) || File.Exists(layout.LegacyHookTargetPath),
                HookDllCompatible = HasCurrentHookAssembly(layout.HookTargetPath),
                BackupPresent = layout.HasCurrentBackup || layout.HasLegacyBackup,
            };

            using (var module = ModuleDefMD.Load(layout.ServiceLibPath))
            {
                PopulateCompatibility(patchStatus, module);

                if (patchStatus.IsCompatible)
                {
                    var serviceType = module.Types.First(type => type.FullName == GpgConstants.ServiceTypeName);
                    var availableMethod = ServiceLibPatcher.FindTargetMethod(serviceType, GpgConstants.AvailableSettingsMethodName);
                    var launchMethod = ServiceLibPatcher.FindTargetMethod(serviceType, GpgConstants.LaunchSettingsMethodName);
                    var monitorDisplayMethod = ServiceLibPatcher.FindMonitorDisplayMethod(serviceType, GpgConstants.MonitorDisplayMethodName);
                    var runtimeDisplayMethod = ServiceLibPatcher.FindRuntimeDisplaySettingsMethod(serviceType, GpgConstants.RuntimeDisplaySettingsMethodName);
                    var virtualGuestDisplayMethod = ServiceLibPatcher.FindVirtualGuestDisplayMoveNextMethod(module);
                    var showWindowRequestMethod = ServiceLibPatcher.FindShowWindowRequestMethod(module);
                    var sharpeningGetter = ServiceLibPatcher.FindSharpeningFilterGetter(module);
                    var sharpeningRequestMethod = ServiceLibPatcher.FindSharpeningFilterRequestMethod(module);
                    var accountLimitMethods = ServiceLibPatcher.FindAccountLimitBypassMethods(module);
                    var openDeepLinkMethod = ServiceLibPatcher.FindOpenDeepLinkMethod(module);
                    var exactLaunchMethod = ServiceLibPatcher.FindExactLaunchMethod(module);

                    patchStatus.AvailableSettingsPatched = ServiceLibPatcher.HasAnyHookCall(
                        availableMethod,
                        GpgConstants.PatchAvailableSettingsMethod);
                    patchStatus.LaunchSettingsPatched = ServiceLibPatcher.HasAnyHookCall(
                        launchMethod,
                        GpgConstants.PatchAndroidDisplaySettingsMethod);
                    patchStatus.MonitorDisplayPatched = ServiceLibPatcher.HasHookCall(
                        monitorDisplayMethod,
                        GpgConstants.PatchMonitorDisplaySizeMethod);
                    patchStatus.RuntimeDisplaySettingsPatched = ServiceLibPatcher.HasHookCall(
                        runtimeDisplayMethod,
                        GpgConstants.PatchRuntimeAndroidDisplaySettingsMethod);
                    patchStatus.VirtualGuestDisplayPatched = ServiceLibPatcher.HasHookCall(
                        virtualGuestDisplayMethod,
                        GpgConstants.PatchAddGuestDisplayRequestMethod);
                    patchStatus.ShowWindowRequestPatched = ServiceLibPatcher.HasHookCall(
                        showWindowRequestMethod,
                        GpgConstants.PatchShowWindowRequestMethod);
                    patchStatus.SharpeningFilterPatched =
                        ServiceLibPatcher.IsConstantTrueMethod(sharpeningGetter)
                        && ServiceLibPatcher.HasForcedSharpeningFilterRequest(sharpeningRequestMethod);
                    patchStatus.AccountLimitBypassPatched =
                        ServiceLibPatcher.HasAccountLimitBypass(accountLimitMethods);
                    patchStatus.AddAccountDeepLinkPatched =
                        ServiceLibPatcher.HasAddAccountDeepLink(openDeepLinkMethod);
                    patchStatus.ExactInstanceLaunchPatched =
                        ServiceLibPatcher.HasExactInstanceLaunch(exactLaunchMethod);
                }

                patchStatus.HookAssemblyReferencePresent = module.GetAssemblyRefs()
                    .Any(reference =>
                        string.Equals(reference.Name, GpgConstants.HookAssemblyName, StringComparison.Ordinal)
                        || string.Equals(reference.Name, GpgConstants.LegacyHookAssemblyName, StringComparison.Ordinal));
            }

            patchStatus.PhenotypeOverrideValue = ReadPhenotypeOverride(layout.ServiceConfigPath);
            patchStatus.PhenotypeOverridePresent = !string.IsNullOrWhiteSpace(patchStatus.PhenotypeOverrideValue);

            return patchStatus;
        }

        private static void PopulateCompatibility(PatchStatus patchStatus, ModuleDefMD module)
        {
            var serviceType = module.Types.FirstOrDefault(type => type.FullName == GpgConstants.ServiceTypeName);
            if (serviceType == null)
            {
                patchStatus.IsCompatible = false;
                patchStatus.CompatibilityState = "TargetTypeMissing";
                patchStatus.CompatibilityMessage = "Could not find AppSessionScope in ServiceLib.dll.";
                return;
            }

            MethodDef availableMethod;
            try
            {
                availableMethod = ServiceLibPatcher.FindTargetMethod(serviceType, GpgConstants.AvailableSettingsMethodName);
            }
            catch (FriendlyException ex)
            {
                patchStatus.IsCompatible = false;
                patchStatus.CompatibilityState = "AvailableSettingsIncompatible";
                patchStatus.CompatibilityMessage = ex.Message;
                return;
            }

            MethodDef launchMethod;
            try
            {
                launchMethod = ServiceLibPatcher.FindTargetMethod(serviceType, GpgConstants.LaunchSettingsMethodName);
            }
            catch (FriendlyException ex)
            {
                patchStatus.IsCompatible = false;
                patchStatus.CompatibilityState = "LaunchSettingsIncompatible";
                patchStatus.CompatibilityMessage = ex.Message;
                return;
            }

            MethodDef monitorDisplayMethod;
            try
            {
                monitorDisplayMethod = ServiceLibPatcher.FindMonitorDisplayMethod(serviceType, GpgConstants.MonitorDisplayMethodName);
            }
            catch (FriendlyException ex)
            {
                patchStatus.IsCompatible = false;
                patchStatus.CompatibilityState = "MonitorDisplayIncompatible";
                patchStatus.CompatibilityMessage = ex.Message;
                return;
            }

            MethodDef runtimeDisplayMethod;
            try
            {
                runtimeDisplayMethod = ServiceLibPatcher.FindRuntimeDisplaySettingsMethod(serviceType, GpgConstants.RuntimeDisplaySettingsMethodName);
            }
            catch (FriendlyException ex)
            {
                patchStatus.IsCompatible = false;
                patchStatus.CompatibilityState = "RuntimeDisplaySettingsIncompatible";
                patchStatus.CompatibilityMessage = ex.Message;
                return;
            }

            try
            {
                ServiceLibPatcher.FindSharpeningFilterGetter(module);
                ServiceLibPatcher.FindSharpeningFilterRequestMethod(module);
            }
            catch (FriendlyException ex)
            {
                patchStatus.IsCompatible = false;
                patchStatus.CompatibilityState = "SharpeningFilterIncompatible";
                patchStatus.CompatibilityMessage = ex.Message;
                return;
            }

            try
            {
                ServiceLibPatcher.FindVirtualGuestDisplayMoveNextMethod(module);
                ServiceLibPatcher.FindShowWindowRequestMethod(module);
            }
            catch (FriendlyException ex)
            {
                patchStatus.IsCompatible = false;
                patchStatus.CompatibilityState = "VirtualViewportIncompatible";
                patchStatus.CompatibilityMessage = ex.Message;
                return;
            }

            try
            {
                ServiceLibPatcher.FindAccountLimitBypassMethods(module);
            }
            catch (FriendlyException ex)
            {
                patchStatus.IsCompatible = false;
                patchStatus.CompatibilityState = "AccountLimitBypassIncompatible";
                patchStatus.CompatibilityMessage = ex.Message;
                return;
            }

            try
            {
                ServiceLibPatcher.FindOpenDeepLinkMethod(module);
            }
            catch (FriendlyException ex)
            {
                patchStatus.IsCompatible = false;
                patchStatus.CompatibilityState = "AddAccountDeepLinkIncompatible";
                patchStatus.CompatibilityMessage = ex.Message;
                return;
            }

            try
            {
                ServiceLibPatcher.FindExactLaunchMethod(module);
            }
            catch (FriendlyException ex)
            {
                patchStatus.IsCompatible = false;
                patchStatus.CompatibilityState = "ExactInstanceLaunchIncompatible";
                patchStatus.CompatibilityMessage = ex.Message;
                return;
            }

            if (ServiceLibPatcher.HasLegacyHookCall(availableMethod, GpgConstants.PatchAvailableSettingsMethod)
                || ServiceLibPatcher.HasLegacyHookCall(launchMethod, GpgConstants.PatchAndroidDisplaySettingsMethod)
                || ServiceLibPatcher.HasLegacyHookCall(monitorDisplayMethod, GpgConstants.PatchMonitorDisplaySizeMethod)
                || ServiceLibPatcher.HasLegacyHookCall(runtimeDisplayMethod, GpgConstants.PatchRuntimeAndroidDisplaySettingsMethod))
            {
                patchStatus.IsCompatible = false;
                patchStatus.CompatibilityState = "LegacyPatchDetected";
                patchStatus.CompatibilityMessage = "A legacy pre-rename patch was detected. Restore the original files first, then apply the current GPG Patcher build.";
                return;
            }

            patchStatus.IsCompatible = true;
            patchStatus.CompatibilityState = "Compatible";
            patchStatus.CompatibilityMessage = "Target methods and signatures match; safe to patch this build.";
        }

        private static bool HasCurrentHookAssembly(string hookPath)
        {
            if (!File.Exists(hookPath))
            {
                return false;
            }

            try
            {
                using (var module = ModuleDefMD.Load(hookPath))
                {
                    var hookType = module.Types.FirstOrDefault(candidate =>
                        string.Equals(candidate.Namespace, GpgConstants.HookTypeNamespace, StringComparison.Ordinal)
                        && string.Equals(candidate.Name, GpgConstants.HookTypeName, StringComparison.Ordinal));

                    return hookType != null
                        && HasMethod(hookType, GpgConstants.PatchAvailableSettingsMethod)
                        && HasMethod(hookType, GpgConstants.PatchAndroidDisplaySettingsMethod)
                        && HasMethod(hookType, GpgConstants.PatchMonitorDisplaySizeMethod)
                        && HasMethod(hookType, GpgConstants.PatchRuntimeAndroidDisplaySettingsMethod)
                        && HasMethod(hookType, GpgConstants.PatchAddGuestDisplayRequestMethod)
                        && HasMethod(hookType, GpgConstants.PatchShowWindowRequestMethod)
                        && HasMethod(hookType, GpgConstants.PatchOnboardedAccountCountMethod);
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool HasMethod(TypeDef type, string methodName)
        {
            return type.Methods.Any(candidate =>
                string.Equals(candidate.Name, methodName, StringComparison.Ordinal)
                && candidate.MethodSig != null);
        }

        private static string ReadPhenotypeOverride(string configPath)
        {
            var document = XDocument.Load(configPath);
            var setting = document
                .Descendants("setting")
                .FirstOrDefault(element => string.Equals(
                    (string)element.Attribute("name"),
                    GpgConstants.PhenotypeSettingName,
                    StringComparison.Ordinal));

            var valueElement = setting == null ? null : setting.Element("value");
            return valueElement == null ? string.Empty : valueElement.Value;
        }
    }
}
