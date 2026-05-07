namespace GpgPatcher
{
    public static class GpgConstants
    {
        public const string TargetPackageName = "com.gof.global";
        public const int TargetWidth = 2160;
        public const int TargetHeight = 3840;

        public static string TargetResolutionLabel
        {
            get { return TargetWidth + "x" + TargetHeight; }
        }
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
        public const string ServiceTypeName = "Google.Hpe.Service.AppSession.AppSessionScope";
        public const string KiwiEmulatorConfigurationTypeName = "Google.Hpe.Service.KiwiEmulator.KiwiEmulatorConfiguration";
        public const string EmulatorReadyControllerTypeName = "Google.Hpe.Service.Emulator.EmulatorReadyController";
        public const string AvailableSettingsMethodName = "GetAvailableAndroidDisplaySettings";
        public const string LaunchSettingsMethodName = "GetAndroidDisplaySettingsOnGameLaunch";
        public const string MonitorDisplayMethodName = "GetMonitorDisplaySize";
        public const string RuntimeDisplaySettingsMethodName = "TrySetAndroidDisplayAsync";
        public const string EnableSharpeningFilterGetterName = "get_EnableSharpeningFilter";
        public const string EnableSharpeningFilterRequestMethodName = "EnableSharpeningFilterAsync";
        public const string SharpeningFilterRequestTypeName = "Google.Hpe.Service.V1.SharpeningFilterRequest";
        public const string LaunchGameRequestFieldName = "_launchGameRequest";
        public const string ServiceLogFileName = "Service.log";
        public const string AndroidSerialLogFileName = "AndroidSerial.log";
        public const string PhenotypeSettingName = "PhenotypeFlagOverrideJson";
    }
}
