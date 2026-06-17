using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using BrainIn.DevTools.Editor.Validation.Core;

namespace BrainIn.DevTools.Editor.Validation.Rules
{
    /// <summary>
    /// Validates the BrainIn localization file required by Unity tasks.
    /// </summary>
    public sealed class LocalizationRule : IValidationRule
    {
        private const string LocaleAssetPath = "Assets/StreamingAssets/ProgramData/Locale/locale.xml";
        private const string LocaleRelativePath = "StreamingAssets/ProgramData/Locale/locale.xml";
        private const string ExpectedRootElementName = "locale";

        private static readonly string[] RequiredLanguageCodes =
        {
            "cs",
            "en",
            "de"
        };

        private static readonly Regex PlaceholderRegex = new Regex(
            @"\{(\d+)(:[^}]*)?\}",
            RegexOptions.Compiled
        );

        /// <summary>
        /// Gets the display name of the validation rule.
        /// </summary>
        public string Name => "Localization validator";

        /// <summary>
        /// Validates the BrainIn localization XML file.
        /// </summary>
        /// <param name="context">Validation context containing project paths and shared validation data.</param>
        /// <returns>Validation results describing detected localization issues.</returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext context)
        {
            var results = new List<ValidationResult>();
            var localeFullPath = GetLocaleFullPath(context);

            if (!File.Exists(localeFullPath))
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    Name,
                    $"Localization file was not found. Expected location: {LocaleAssetPath}",
                    LocaleAssetPath
                ));

                return results;
            }

            XDocument document;

            try
            {
                document = XDocument.Load(localeFullPath, LoadOptions.SetLineInfo);
            }
            catch (XmlException exception)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    Name,
                    $"Localization XML is not valid. Line {exception.LineNumber}, position {exception.LinePosition}: {exception.Message}",
                    LocaleAssetPath
                ));

                return results;
            }
            catch (Exception exception)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    Name,
                    $"Localization file could not be loaded: {exception.Message}",
                    LocaleAssetPath
                ));

                return results;
            }

            ValidateRootElement(document, results);

            var localizationEntries = FindLocalizationEntries(document).ToList();

            if (localizationEntries.Count == 0)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    Name,
                    "Localization file does not contain any localization entries.",
                    LocaleAssetPath
                ));

                return results;
            }

            ValidateDuplicateLocalizationKeys(localizationEntries, results);

            foreach (var entry in localizationEntries)
            {
                ValidateLocalizationEntry(entry, results);
            }

            if (results.Count == 0)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Info,
                    Name,
                    "Localization file contains all required languages and looks valid.",
                    LocaleAssetPath
                ));
            }

            return results;
        }

        /// <summary>
        /// Gets the absolute path to the expected localization file.
        /// </summary>
        /// <param name="context">Validation context containing the Unity Assets path.</param>
        /// <returns>Absolute path to the localization file.</returns>
        private static string GetLocaleFullPath(ValidationContext context)
        {
            return Path.Combine(
                context.AssetsPath,
                LocaleRelativePath.Replace("/", Path.DirectorySeparatorChar.ToString())
            );
        }

        /// <summary>
        /// Validates whether the XML document has the expected root element.
        /// </summary>
        /// <param name="document">Loaded localization XML document.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        private void ValidateRootElement(XDocument document, List<ValidationResult> results)
        {
            if (document.Root == null)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    Name,
                    "Localization XML does not contain a root element.",
                    LocaleAssetPath
                ));

                return;
            }

            if (!string.Equals(document.Root.Name.LocalName, ExpectedRootElementName, StringComparison.Ordinal))
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    Name,
                    $"Localization XML root element should be <{ExpectedRootElementName}>, but found <{document.Root.Name.LocalName}>.",
                    LocaleAssetPath
                ));
            }
        }

        /// <summary>
        /// Finds XML elements that represent localization entries.
        /// </summary>
        /// <param name="document">Loaded localization XML document.</param>
        /// <returns>Localization entry elements.</returns>
        private static IEnumerable<XElement> FindLocalizationEntries(XDocument document)
        {
            if (document.Root == null)
            {
                return Enumerable.Empty<XElement>();
            }

            return document
                .Root
                .Descendants()
                .Where(IsLocalizationEntry);
        }

        /// <summary>
        /// Determines whether an XML element contains direct language child elements.
        /// </summary>
        /// <param name="element">XML element to inspect.</param>
        /// <returns>True if the element represents a localization entry; otherwise false.</returns>
        private static bool IsLocalizationEntry(XElement element)
        {
            return element
                .Elements()
                .Any(child => IsRequiredLanguageCode(child.Name.LocalName));
        }

        /// <summary>
        /// Validates one localization entry.
        /// </summary>
        /// <param name="entry">Localization entry XML element.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        private void ValidateLocalizationEntry(XElement entry, List<ValidationResult> results)
        {
            ValidateRequiredLanguages(entry, results);
            ValidateDuplicateLanguages(entry, results);
            ValidateUnexpectedChildren(entry, results);
            ValidateEmptyTranslations(entry, results);
            ValidateNestedTranslationElements(entry, results);
            ValidatePlaceholderConsistency(entry, results);
        }

        /// <summary>
        /// Validates whether all required language elements are present in a localization entry.
        /// </summary>
        /// <param name="entry">Localization entry XML element.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        private void ValidateRequiredLanguages(XElement entry, List<ValidationResult> results)
        {
            var availableLanguages = entry
                .Elements()
                .Select(element => element.Name.LocalName)
                .ToHashSet(StringComparer.Ordinal);

            results.AddRange(from languageCode in RequiredLanguageCodes
                where !availableLanguages.Contains(languageCode)
                select new ValidationResult(ValidationSeverity.Error, Name,
                    $"{FormatLocation(entry)} Localization key '{GetLocalizationKeyPath(entry)}' is missing required language '{languageCode}'.",
                    LocaleAssetPath));
        }

        /// <summary>
        /// Validates whether a localization entry does not contain duplicate language elements.
        /// </summary>
        /// <param name="entry">Localization entry XML element.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        private void ValidateDuplicateLanguages(XElement entry, List<ValidationResult> results)
        {
            var duplicateLanguages = entry
                .Elements()
                .Where(element => IsRequiredLanguageCode(element.Name.LocalName))
                .GroupBy(element => element.Name.LocalName)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key);

            results.AddRange(duplicateLanguages.Select(languageCode => new ValidationResult(ValidationSeverity.Error,
                Name,
                $"{FormatLocation(entry)} Localization key '{GetLocalizationKeyPath(entry)}' contains duplicate language '{languageCode}'.",
                LocaleAssetPath)));
        }

        /// <summary>
        /// Validates whether a localization entry contains only supported language child elements.
        /// </summary>
        /// <param name="entry">Localization entry XML element.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        private void ValidateUnexpectedChildren(XElement entry, List<ValidationResult> results)
        {
            var unexpectedChildren = entry
                .Elements()
                .Where(element => !IsRequiredLanguageCode(element.Name.LocalName));

            results.AddRange(unexpectedChildren.Select(child => new ValidationResult(ValidationSeverity.Warning, Name,
                $"{FormatLocation(child)} Localization key '{GetLocalizationKeyPath(entry)}' contains unsupported child element '<{child.Name.LocalName}>'. Expected only language elements: cs, en, de.",
                LocaleAssetPath)));
        }

        /// <summary>
        /// Validates whether required translations are not empty.
        /// </summary>
        /// <param name="entry">Localization entry XML element.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        private void ValidateEmptyTranslations(XElement entry, List<ValidationResult> results)
        {
            var requiredLanguageElements = entry
                .Elements()
                .Where(element => IsRequiredLanguageCode(element.Name.LocalName));

            foreach (var languageElement in requiredLanguageElements)
            {
                if (!string.IsNullOrWhiteSpace(languageElement.Value))
                {
                    continue;
                }

                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    Name,
                    $"{FormatLocation(languageElement)} Localization key '{GetLocalizationKeyPath(entry)}' has empty translation for language '{languageElement.Name.LocalName}'.",
                    LocaleAssetPath
                ));
            }
        }

        /// <summary>
        /// Validates whether language elements contain plain text instead of nested XML elements.
        /// </summary>
        /// <param name="entry">Localization entry XML element.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        private void ValidateNestedTranslationElements(XElement entry, List<ValidationResult> results)
        {
            var languageElementsWithNestedElements = entry
                .Elements()
                .Where(element => IsRequiredLanguageCode(element.Name.LocalName))
                .Where(element => element.Elements().Any());

            foreach (var languageElement in languageElementsWithNestedElements)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    Name,
                    $"{FormatLocation(languageElement)} Translation for key '{GetLocalizationKeyPath(entry)}' and language '{languageElement.Name.LocalName}' contains nested XML elements. Rich-text tags should be escaped, for example &lt;b&gt;.",
                    LocaleAssetPath
                ));
            }
        }

        /// <summary>
        /// Validates whether all translations in one localization entry use the same string placeholders.
        /// </summary>
        /// <param name="entry">Localization entry XML element.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        private void ValidatePlaceholderConsistency(XElement entry, List<ValidationResult> results)
        {
            var placeholdersByLanguage = entry
                .Elements()
                .Where(element => IsRequiredLanguageCode(element.Name.LocalName))
                .Where(element => !string.IsNullOrWhiteSpace(element.Value))
                .ToDictionary(
                    element => element.Name.LocalName,
                    element => GetPlaceholders(element.Value),
                    StringComparer.Ordinal
                );

            if (placeholdersByLanguage.Count <= 1)
            {
                return;
            }

            var referenceLanguage = RequiredLanguageCodes.First(placeholdersByLanguage.ContainsKey);
            var referencePlaceholders = placeholdersByLanguage[referenceLanguage];

            results.AddRange(from pair in placeholdersByLanguage
                where pair.Key != referenceLanguage
                where !referencePlaceholders.SetEquals(pair.Value)
                select new ValidationResult(ValidationSeverity.Error, Name,
                    $"{FormatLocation(entry)} Localization key '{GetLocalizationKeyPath(entry)}' has inconsistent placeholders. Language '{referenceLanguage}' uses [{FormatPlaceholders(referencePlaceholders)}], but language '{pair.Key}' uses [{FormatPlaceholders(pair.Value)}].",
                    LocaleAssetPath));
        }

        /// <summary>
        /// Validates whether localization keys are unique within the file.
        /// </summary>
        /// <param name="entries">Localization entries to inspect.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        private void ValidateDuplicateLocalizationKeys(
            IReadOnlyList<XElement> entries,
            List<ValidationResult> results)
        {
            var duplicateKeys = entries
                .GroupBy(GetLocalizationKeyPath)
                .Where(group => group.Count() > 1);

            results.AddRange(duplicateKeys.Select(duplicateKey => new ValidationResult(ValidationSeverity.Error, Name,
                $"Localization key '{duplicateKey.Key}' is defined multiple times.", LocaleAssetPath)));
        }

        /// <summary>
        /// Determines whether a language code is required by BrainIn.
        /// </summary>
        /// <param name="languageCode">Language code to inspect.</param>
        /// <returns>True if the language code is required; otherwise false.</returns>
        private static bool IsRequiredLanguageCode(string languageCode)
        {
            return RequiredLanguageCodes.Contains(languageCode, StringComparer.Ordinal);
        }

        /// <summary>
        /// Extracts string formatting placeholders from a localized text.
        /// </summary>
        /// <param name="text">Localized text to inspect.</param>
        /// <returns>Set of placeholder indexes used in the text.</returns>
        private static HashSet<string> GetPlaceholders(string text)
        {
            return PlaceholderRegex
                .Matches(text)
                .Select(match => match.Groups[1].Value)
                .ToHashSet(StringComparer.Ordinal);
        }

        /// <summary>
        /// Creates a readable localization key path from an XML element.
        /// </summary>
        /// <param name="entry">Localization entry XML element.</param>
        /// <returns>Readable localization key path.</returns>
        private static string GetLocalizationKeyPath(XElement entry)
        {
            return string.Join(
                "/",
                entry
                    .AncestorsAndSelf()
                    .Reverse()
                    .Where(element => element.Parent != null)
                    .Select(element => element.Name.LocalName)
            );
        }

        /// <summary>
        /// Formats a placeholder set for display in validation messages.
        /// </summary>
        /// <param name="placeholders">Placeholder set to format.</param>
        /// <returns>Readable placeholder list.</returns>
        private static string FormatPlaceholders(HashSet<string> placeholders)
        {
            if (placeholders.Count == 0)
                return "none";

            return string.Join(
                ", ",
                placeholders
                    .OrderBy(placeholder => placeholder)
                    .Select(placeholder => $"{{{placeholder}}}")
            );
        }

        /// <summary>
        /// Formats XML line information for validation messages.
        /// </summary>
        /// <param name="element">XML element to format location for.</param>
        /// <returns>Readable location prefix if line information is available; otherwise empty string.</returns>
        private static string FormatLocation(XElement element)
        {
            if (element is not IXmlLineInfo lineInfo || !lineInfo.HasLineInfo())
            {
                return string.Empty;
            }

            return $"Line {lineInfo.LineNumber}: ";
        }
    }
}