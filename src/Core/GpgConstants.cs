using System;

namespace GpgPatcher
{
    public static class GpgConstants
    {
        public const string TargetPackageName = "com.gof.global";
        public const string AppDataDirectoryName = "GpgPatcher";
        public const string LegacyAppDataDirectoryName = "GpgResPoC";
        public const string HookAssemblyName = "GpgPatcher.Hooks";
        public const string HookAssemblyFileName = "GpgPatcher.Hooks.dll";
        public const string HookTypeNamespace = "GpgPatcher.Hooks";
        public const string LegacyHookAssemblyName = "GpgResPoc.Hooks";
        public const string LegacyHookAssemblyFileName = "GpgResPoc.Hooks.dll";
        public const string LegacyHookTypeNamespace = "GpgResPoc.Hooks";
        public const string HookTypeName = "DisplaySettingsHooks";
        public const string PatchAvailableSettingsMethod = "PatchAvailableSettings";
        public const string PatchAndroidDisplaySettingsMethod = "PatchAndroidDisplaySettings";
        public const string PatchMonitorDisplaySizeMethod = "PatchMonitorDisplaySize";
        public const string PatchRuntimeAndroidDisplaySettingsMethod = "PatchRuntimeAndroidDisplaySettings";
        public const string PatchAddGuestDisplayRequestMethod = "PatchAddGuestDisplayRequest";
        public const string PatchShowWindowRequestMethod = "PatchShowWindowRequest";
        public const string PatchOnboardedAccountCountMethod = "PatchOnboardedAccountCount";
        public const string AddAccountProtocolUrl = "googleplaygames://deeplink/gpg-patcher-add-account";
        public const string LaunchProtocolUrlPrefix = "googleplaygames://launch/?id=com.gof.global&lid=1&pid=";
        public const string ExactLaunchProtocolUrlPrefix = "googleplaygames://launch/?id=com.gof.global&lid=1&pid=0&aid=";
        public const string ServiceTypeName = "Google.Hpe.Service.AppSession.AppSessionScope";
        public const string EmulatorSurfaceScopeTypeName = "Google.Hpe.Service.AppSession.EmulatorSurface.EmulatorSurfaceScope";
        public const string EmulatorSurfaceReadyControllerTypeName = "Google.Hpe.Service.AppSession.EmulatorSurface.EmulatorSurfaceReadyController";
        public const string ClientControllerTypeName = "Google.Hpe.Service.Client.ClientController";
        public const string AppLauncherCommandHandlerTypeName = "Google.Hpe.Service.AppLauncher.AppLauncherCommandHandler";
        public const string LocalStateModuleTypeName = "Google.Hpe.Service.Client.LocalStateModule";
        public const string AccountsInfoUpdaterMoveNextTypeName = "Google.Hpe.Service.Client.LocalStates.AccountsInfoUpdater/<UpdateAccountsInfoAsync>d__4";
        public const string GlobalStateAccountCountMutatorTypeName = "Google.Hpe.Service.GlobalState.GlobalStateModule/<>c__DisplayClass24_0";
        public const string KiwiEmulatorConfigurationTypeName = "Google.Hpe.Service.KiwiEmulator.KiwiEmulatorConfiguration";
        public const string EmulatorReadyControllerTypeName = "Google.Hpe.Service.Emulator.EmulatorReadyController";
        public const string AvailableSettingsMethodName = "GetAvailableAndroidDisplaySettings";
        public const string LaunchSettingsMethodName = "GetAndroidDisplaySettingsOnGameLaunch";
        public const string MonitorDisplayMethodName = "GetMonitorDisplaySize";
        public const string RuntimeDisplaySettingsMethodName = "TrySetAndroidDisplayAsync";
        public const string WaitUntilDisplayAddedAsyncMethodName = "WaitUntilDisplayAddedAsync";
        public const string ShowAsyncMethodName = "ShowAsync";
        public const string UpdateAccountsInfoMethodName = "UpdateAccountsInfo";
        public const string MoveNextMethodName = "MoveNext";
        public const string GlobalStateSetOnboardedAccountsMethodName = "<ObserveAccountsStateAsync>b__2";
        public const string ObserveAccountsStateAsyncMethodName = "ObserveAccountsStateAsync";
        public const string EnableSharpeningFilterGetterName = "get_EnableSharpeningFilter";
        public const string EnableSharpeningFilterRequestMethodName = "EnableSharpeningFilterAsync";
        public const string OpenDeepLinkMethodName = "OpenDeepLinkAsync";
        public const string HandleCommandMethodName = "HandleCommand";
        public const string AddAccountMethodName = "AddAccountAsync";
        public const string AccountsInfoTypeName = "Google.Hpe.Client.V1.AccountsInfo";
        public const string AccountsStateTypeName = "Google.Hpe.Service.Accounts.AccountsState";
        public const string GlobalStateTypeName = "Google.Play.Games.Metrics.GlobalState";
        public const string SharpeningFilterRequestTypeName = "Google.Hpe.Service.V1.SharpeningFilterRequest";
        public const string LaunchGameRequestFieldName = "_launchGameRequest";
        public const string AndroidAppLibraryIdParameterName = "aid";
        public const string StateMachineThisFieldName = "<>4__this";
        public const string ServiceLogFileName = "Service.log";
        public const string AndroidSerialLogFileName = "AndroidSerial.log";
        public const string PhenotypeSettingName = "PhenotypeFlagOverrideJson";

        public static string BuildLaunchProtocolUrl(int profileSlot)
        {
            return LaunchProtocolUrlPrefix + profileSlot;
        }

        public static string BuildExactLaunchProtocolUrl(string androidAppLibraryId)
        {
            if (string.IsNullOrWhiteSpace(androidAppLibraryId))
            {
                throw new ArgumentException("Android app library ID is required.", "androidAppLibraryId");
            }

            return ExactLaunchProtocolUrlPrefix + Uri.EscapeDataString(androidAppLibraryId.Trim());
        }
    }
}
