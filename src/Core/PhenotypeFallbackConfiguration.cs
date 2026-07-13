using System;
using System.Linq;
using System.Xml.Linq;

namespace GpgPatcher
{
    internal static class PhenotypeFallbackConfiguration
    {
        private const string UhdOverride =
            "{\"Enable4KUhdResolution\":true,\"GoldTierDefaultToUse4KUhd\":true,\"SilverTierDefaultToUse4KUhd\":true}";

        public static void Apply(string configPath)
        {
            var document = XDocument.Load(configPath);
            var valueElement = FindValueElement(document);
            valueElement.Value = UhdOverride;
            document.Save(configPath);
        }

        public static void Clear(string configPath)
        {
            var document = XDocument.Load(configPath);
            var valueElement = FindValueElement(document);
            valueElement.Value = string.Empty;
            document.Save(configPath);
        }

        private static XElement FindValueElement(XDocument document)
        {
            var setting = document
                .Descendants("setting")
                .FirstOrDefault(element => string.Equals(
                    (string)element.Attribute("name"),
                    GpgConstants.PhenotypeSettingName,
                    StringComparison.Ordinal));
            var valueElement = setting == null ? null : setting.Element("value");
            if (valueElement == null)
            {
                throw new FriendlyException("PhenotypeFlagOverrideJson is missing its <value> node in Service.exe.config.");
            }

            return valueElement;
        }
    }
}
