using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Shell;
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;

namespace OpenEditors.VisualStudio.Tests
{
    [TestClass]
    public class DocumentStatusToBrushConverterTests
    {
        private const string ConverterTypeName = "OpenEditors.VisualStudio.Vsix.ToolWindows.DocumentStatusToBrushConverter";
        private const string GitConverterTypeName = "OpenEditors.VisualStudio.Vsix.ToolWindows.GitStatusToBrushConverter";
        private const string OpenDocumentItemTypeName = "OpenEditors.VisualStudio.Vsix.Models.OpenDocumentItem";
        private const string GitDocumentStatusTypeName = "OpenEditors.VisualStudio.Vsix.Models.GitDocumentStatus";

        [TestMethod]
        public void Convert_WithNonDocumentValue_ReturnsTransparentBrush()
        {
            var converter = CreateConverter(out var converterType);
            var convert = converterType.GetMethod("Convert", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(convert);

            try
            {
                var result = convert.Invoke(converter, new object[] { "invalid", null, null, CultureInfo.InvariantCulture });
                StringAssert.Contains(result?.ToString() ?? string.Empty, "00FFFFFF");
            }
            catch (TargetInvocationException ex) when (ex.InnerException is FileNotFoundException)
            {
                Assert.Inconclusive("Dependências do Visual Studio Shell não estão disponíveis no runner de testes.");
            }
        }

        [TestMethod]
        public void Convert_WithDocumentWithErrors_ReturnsToolWindowBorderBrush()
        {
            EnsureApplication();
            var expectedBrush = new SolidColorBrush(Colors.Red);
            Application.Current.Resources[VsBrushes.ToolWindowBorderKey] = expectedBrush;

            var converter = CreateConverter(out var converterType);
            var convert = converterType.GetMethod("Convert", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(convert);

            var item = CreateDocumentItem(hasErrors: true, gitStatusName: "Clean");
            var result = convert.Invoke(converter, new object[] { item, null, null, CultureInfo.InvariantCulture });

            Assert.AreSame(expectedBrush, result);
        }

        [TestMethod]
        public void Convert_WithModifiedDocumentWithoutErrors_UsesGitConverterBrush()
        {
            EnsureApplication();
            var expectedBrush = new SolidColorBrush(Colors.Blue);
            Application.Current.Resources[VsBrushes.ToolWindowTextKey] = expectedBrush;

            var converter = CreateConverter(out var converterType);
            var convert = converterType.GetMethod("Convert", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(convert);

            var item = CreateDocumentItem(hasErrors: false, gitStatusName: "Modified");
            var result = convert.Invoke(converter, new object[] { item, null, null, CultureInfo.InvariantCulture });

            Assert.AreSame(expectedBrush, result);
        }

        [TestMethod]
        public void Convert_WithCleanDocumentWithoutErrors_ReturnsTransparentBrush()
        {
            var converter = CreateConverter(out var converterType);
            var convert = converterType.GetMethod("Convert", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(convert);

            var item = Activator.CreateInstance(VsixTestAssemblyLoader.Load().GetType(OpenDocumentItemTypeName, throwOnError: true));
            var itemType = item.GetType();
            itemType.GetProperty("HasErrors", BindingFlags.Instance | BindingFlags.Public)?.SetValue(item, false);

            var gitStatusType = VsixTestAssemblyLoader.Load().GetType(GitDocumentStatusTypeName, throwOnError: true);
            var clean = Enum.Parse(gitStatusType, "Clean");
            itemType.GetProperty("GitStatus", BindingFlags.Instance | BindingFlags.Public)?.SetValue(item, clean);

            try
            {
                var result = convert.Invoke(converter, new object[] { item, null, null, CultureInfo.InvariantCulture });
                StringAssert.Contains(result?.ToString() ?? string.Empty, "00FFFFFF");
            }
            catch (TargetInvocationException ex) when (ex.InnerException is FileNotFoundException)
            {
                Assert.Inconclusive("Dependências do Visual Studio Shell não estão disponíveis no runner de testes.");
            }
        }

        [TestMethod]
        public void ConvertBack_AlwaysThrowsNotSupportedException()
        {
            var converter = CreateConverter(out var converterType);
            var convertBack = converterType.GetMethod("ConvertBack", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(convertBack);

            var ex = Assert.ThrowsException<TargetInvocationException>(() =>
                convertBack.Invoke(converter, new object[] { null, null, null, CultureInfo.InvariantCulture }));

            Assert.IsInstanceOfType(ex.InnerException, typeof(NotSupportedException));
        }

        [TestMethod]
        public void GitConverter_Convert_WithUnknownValue_ReturnsTransparentBrush()
        {
            var converter = CreateGitConverter(out var converterType);
            var convert = converterType.GetMethod("Convert", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(convert);

            var result = convert.Invoke(converter, new object[] { "invalid", null, null, CultureInfo.InvariantCulture });

            StringAssert.Contains(result?.ToString() ?? string.Empty, "00FFFFFF");
        }

        [TestMethod]
        public void GitConverter_Convert_WithAddedStatus_ReturnsToolWindowBackgroundBrush()
        {
            EnsureApplication();
            var expectedBrush = new SolidColorBrush(Colors.Green);
            Application.Current.Resources[VsBrushes.ToolWindowBackgroundKey] = expectedBrush;

            var converter = CreateGitConverter(out var converterType);
            var convert = converterType.GetMethod("Convert", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(convert);

            var gitStatusType = VsixTestAssemblyLoader.Load().GetType(GitDocumentStatusTypeName, throwOnError: true);
            var added = Enum.Parse(gitStatusType, "Added");
            var result = convert.Invoke(converter, new object[] { added, null, null, CultureInfo.InvariantCulture });

            Assert.AreSame(expectedBrush, result);
        }

        [TestMethod]
        public void GitConverter_Convert_WithDeletedStatus_ReturnsToolWindowBorderBrush()
        {
            EnsureApplication();
            var expectedBrush = new SolidColorBrush(Colors.Orange);
            Application.Current.Resources[VsBrushes.ToolWindowBorderKey] = expectedBrush;

            var converter = CreateGitConverter(out var converterType);
            var convert = converterType.GetMethod("Convert", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(convert);

            var gitStatusType = VsixTestAssemblyLoader.Load().GetType(GitDocumentStatusTypeName, throwOnError: true);
            var deleted = Enum.Parse(gitStatusType, "Deleted");
            var result = convert.Invoke(converter, new object[] { deleted, null, null, CultureInfo.InvariantCulture });

            Assert.AreSame(expectedBrush, result);
        }

        [TestMethod]
        public void GitConverter_ConvertBack_AlwaysThrowsNotSupportedException()
        {
            var converter = CreateGitConverter(out var converterType);
            var convertBack = converterType.GetMethod("ConvertBack", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(convertBack);

            var ex = Assert.ThrowsException<TargetInvocationException>(() =>
                convertBack.Invoke(converter, new object[] { null, null, null, CultureInfo.InvariantCulture }));

            Assert.IsInstanceOfType(ex.InnerException, typeof(NotSupportedException));
        }

        private static object CreateConverter(out Type converterType)
        {
            converterType = VsixTestAssemblyLoader.Load().GetType(ConverterTypeName, throwOnError: true);
            return Activator.CreateInstance(converterType);
        }

        private static object CreateGitConverter(out Type converterType)
        {
            converterType = VsixTestAssemblyLoader.Load().GetType(GitConverterTypeName, throwOnError: true);
            return Activator.CreateInstance(converterType);
        }

        private static object CreateDocumentItem(bool hasErrors, string gitStatusName)
        {
            var assembly = VsixTestAssemblyLoader.Load();
            var item = Activator.CreateInstance(assembly.GetType(OpenDocumentItemTypeName, throwOnError: true));
            var itemType = item.GetType();
            itemType.GetProperty("HasErrors", BindingFlags.Instance | BindingFlags.Public)?.SetValue(item, hasErrors);

            var gitStatusType = assembly.GetType(GitDocumentStatusTypeName, throwOnError: true);
            var status = Enum.Parse(gitStatusType, gitStatusName);
            itemType.GetProperty("GitStatus", BindingFlags.Instance | BindingFlags.Public)?.SetValue(item, status);

            return item;
        }

        private static void EnsureApplication()
        {
            if (Application.Current == null)
            {
                new Application();
            }
        }
    }
}
