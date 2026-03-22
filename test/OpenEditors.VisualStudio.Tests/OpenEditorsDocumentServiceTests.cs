using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Runtime.Serialization;

namespace OpenEditors.VisualStudio.Tests
{
    [TestClass]
    public class OpenEditorsDocumentServiceTests
    {
        private const string ServiceTypeName = "OpenEditors.VisualStudio.Vsix.Services.OpenEditorsDocumentService";
        private const string GitStatusServiceTypeName = "OpenEditors.VisualStudio.Vsix.Services.IGitStatusService";
        private const string DocumentErrorServiceTypeName = "OpenEditors.VisualStudio.Vsix.Services.IDocumentErrorService";
        private const string FileIconServiceTypeName = "OpenEditors.VisualStudio.Vsix.Services.IFileIconService";
        private const string ThemeServiceTypeName = "OpenEditors.VisualStudio.Vsix.Services.IThemeService";

        [TestMethod]
        public void Constructor_WhenPackageIsNull_ThrowsArgumentNullException()
        {
            AssertConstructorThrowsForNullDependency(
                dependencies => new object[] { null, dependencies.GitService, dependencies.DocumentErrorService, dependencies.FileIconService, dependencies.ThemeService },
                "package");
        }

        [TestMethod]
        public void Constructor_WhenGitStatusServiceIsNull_ThrowsArgumentNullException()
        {
            AssertConstructorThrowsForNullDependency(
                dependencies => new object[] { dependencies.Package, null, dependencies.DocumentErrorService, dependencies.FileIconService, dependencies.ThemeService },
                "gitStatusService");
        }

        [TestMethod]
        public void Constructor_WhenDocumentErrorServiceIsNull_ThrowsArgumentNullException()
        {
            AssertConstructorThrowsForNullDependency(
                dependencies => new object[] { dependencies.Package, dependencies.GitService, null, dependencies.FileIconService, dependencies.ThemeService },
                "documentErrorService");
        }

        [TestMethod]
        public void Constructor_WhenFileIconServiceIsNull_ThrowsArgumentNullException()
        {
            AssertConstructorThrowsForNullDependency(
                dependencies => new object[] { dependencies.Package, dependencies.GitService, dependencies.DocumentErrorService, null, dependencies.ThemeService },
                "fileIconService");
        }

        [TestMethod]
        public void Constructor_WhenThemeServiceIsNull_ThrowsArgumentNullException()
        {
            AssertConstructorThrowsForNullDependency(
                dependencies => new object[] { dependencies.Package, dependencies.GitService, dependencies.DocumentErrorService, dependencies.FileIconService, null },
                "themeService");
        }

        [TestMethod]
        public void Constructor_WithValidDependencies_InitializesEmptyDocumentsCollection()
        {
            var dependencies = CreateDependencies();
            var constructor = GetServiceConstructor();

            var service = constructor.Invoke(new[]
            {
                dependencies.Package,
                dependencies.GitService,
                dependencies.DocumentErrorService,
                dependencies.FileIconService,
                dependencies.ThemeService,
            });

            Assert.IsNotNull(service);

            var documentsProperty = service.GetType().GetProperty("Documents", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(documentsProperty);

            var documents = documentsProperty.GetValue(service) as ICollection;
            Assert.IsNotNull(documents);
            Assert.AreEqual(0, documents.Count);
        }

        [TestMethod]
        public void ShouldIgnoreDocumentPath_ReturnsTrue_ForCopilotBaselineTempFile()
        {
            var method = GetShouldIgnoreDocumentPathMethod();
            var path = Path.Combine(Path.GetTempPath(), "CopilotBaseline", "abc123", "~GitStatusServiceTests.cs");

            var result = (bool)method.Invoke(null, new object[] { path });

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void ShouldIgnoreDocumentPath_ReturnsFalse_ForRegularWorkspaceFile()
        {
            var method = GetShouldIgnoreDocumentPathMethod();
            var path = Path.Combine(Environment.CurrentDirectory, "test", "OpenEditors.VisualStudio.Tests", "GitStatusServiceTests.cs");

            var result = (bool)method.Invoke(null, new object[] { path });

            Assert.IsFalse(result);
        }

        private static ConstructorInfo GetServiceConstructor()
        {
            var assembly = VsixTestAssemblyLoader.Load();
            var serviceType = assembly.GetType(ServiceTypeName, throwOnError: true);
            var gitType = assembly.GetType(GitStatusServiceTypeName, throwOnError: true);
            var documentErrorType = assembly.GetType(DocumentErrorServiceTypeName, throwOnError: true);
            var fileIconType = assembly.GetType(FileIconServiceTypeName, throwOnError: true);
            var themeType = assembly.GetType(ThemeServiceTypeName, throwOnError: true);

            var constructor = serviceType.GetConstructor(new[]
            {
                typeof(AsyncPackage),
                gitType,
                documentErrorType,
                fileIconType,
                themeType,
            });

            Assert.IsNotNull(constructor);
            return constructor;
        }

        private static MethodInfo GetShouldIgnoreDocumentPathMethod()
        {
            var assembly = VsixTestAssemblyLoader.Load();
            var serviceType = assembly.GetType(ServiceTypeName, throwOnError: true);
            var method = serviceType.GetMethod("ShouldIgnoreDocumentPath", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.IsNotNull(method);
            return method;
        }

        private static ServiceDependencies CreateDependencies()
        {
            var assembly = VsixTestAssemblyLoader.Load();

            var documentErrorType = assembly.GetType(DocumentErrorServiceTypeName, throwOnError: true);
            var fileIconType = assembly.GetType(FileIconServiceTypeName, throwOnError: true);
            var themeType = assembly.GetType(ThemeServiceTypeName, throwOnError: true);
            var gitServiceImplementationType = assembly.GetType("OpenEditors.VisualStudio.Vsix.Services.GitStatusService", throwOnError: true);

            return new ServiceDependencies
            {
                Package = (AsyncPackage)FormatterServices.GetUninitializedObject(typeof(DummyAsyncPackage)),
                GitService = Activator.CreateInstance(gitServiceImplementationType),
                DocumentErrorService = CreateInterfaceProxy(documentErrorType),
                FileIconService = CreateInterfaceProxy(fileIconType),
                ThemeService = CreateInterfaceProxy(themeType),
            };
        }

        private static void AssertConstructorThrowsForNullDependency(Func<ServiceDependencies, object[]> argumentFactory, string expectedParamName)
        {
            var dependencies = CreateDependencies();
            var constructor = GetServiceConstructor();

            var ex = Assert.ThrowsException<TargetInvocationException>(() =>
                constructor.Invoke(argumentFactory(dependencies)));

            var inner = ex.InnerException as ArgumentNullException;
            Assert.IsNotNull(inner);
            Assert.AreEqual(expectedParamName, inner.ParamName);
        }

        private static object CreateInterfaceProxy(Type interfaceType)
        {
            var proxy = new NoOpInterfaceProxy(interfaceType);
            return proxy.GetTransparentProxy();
        }

        private sealed class ServiceDependencies
        {
            public AsyncPackage Package { get; set; }
            public object GitService { get; set; }
            public object DocumentErrorService { get; set; }
            public object FileIconService { get; set; }
            public object ThemeService { get; set; }
        }

        private sealed class DummyAsyncPackage : AsyncPackage
        {
        }

        private sealed class NoOpInterfaceProxy : RealProxy
        {
            public NoOpInterfaceProxy(Type interfaceType)
                : base(interfaceType)
            {
            }

            public override IMessage Invoke(IMessage msg)
            {
                var methodCall = msg as IMethodCallMessage;
                var method = methodCall?.MethodBase as MethodInfo;

                object returnValue = null;
                if (method != null && method.ReturnType != typeof(void) && method.ReturnType.IsValueType)
                {
                    returnValue = Activator.CreateInstance(method.ReturnType);
                }

                return new ReturnMessage(returnValue, null, 0, methodCall?.LogicalCallContext, methodCall);
            }
        }
    }
}
