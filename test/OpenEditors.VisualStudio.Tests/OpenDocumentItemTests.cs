using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

namespace OpenEditors.VisualStudio.Tests
{
    [TestClass]
    public class OpenDocumentItemTests
    {
        private const string OpenDocumentItemTypeName = "OpenEditors.VisualStudio.Vsix.Models.OpenDocumentItem";

        [TestMethod]
        public void IsActive_WhenChanged_RaisesPropertyChangedOnce()
        {
            var item = CreateOpenDocumentItem();
            var notifier = (INotifyPropertyChanged)item;
            var raised = new List<string>();
            notifier.PropertyChanged += (sender, e) => raised.Add(e.PropertyName);

            var property = item.GetType().GetProperty("IsActive", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(property);

            property.SetValue(item, true);
            property.SetValue(item, true);

            Assert.AreEqual(1, raised.Count);
            Assert.AreEqual("IsActive", raised[0]);
        }

        [TestMethod]
        public void IsDirty_WhenChanged_RaisesPropertyChangedOnce()
        {
            var item = CreateOpenDocumentItem();
            var notifier = (INotifyPropertyChanged)item;
            var raised = new List<string>();
            notifier.PropertyChanged += (sender, e) => raised.Add(e.PropertyName);

            var property = item.GetType().GetProperty("IsDirty", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(property);

            property.SetValue(item, true);
            property.SetValue(item, true);

            Assert.AreEqual(1, raised.Count);
            Assert.AreEqual("IsDirty", raised[0]);
        }

        [TestMethod]
        public void HasErrors_WhenChanged_RaisesPropertyChangedOnce()
        {
            var item = CreateOpenDocumentItem();
            var notifier = (INotifyPropertyChanged)item;
            var raised = new List<string>();
            notifier.PropertyChanged += (sender, e) => raised.Add(e.PropertyName);

            var property = item.GetType().GetProperty("HasErrors", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(property);

            property.SetValue(item, true);
            property.SetValue(item, true);

            Assert.AreEqual(1, raised.Count);
            Assert.AreEqual("HasErrors", raised[0]);
        }

        [TestMethod]
        public void GitStatus_WhenChanged_RaisesPropertyChangedOnce()
        {
            var item = CreateOpenDocumentItem();
            var notifier = (INotifyPropertyChanged)item;
            var raised = new List<string>();
            notifier.PropertyChanged += (sender, e) => raised.Add(e.PropertyName);

            var assembly = VsixTestAssemblyLoader.Load();
            var statusType = assembly.GetType("OpenEditors.VisualStudio.Vsix.Models.GitDocumentStatus", throwOnError: true);
            var modified = Enum.Parse(statusType, "Modified");

            var property = item.GetType().GetProperty("GitStatus", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(property);

            property.SetValue(item, modified);
            property.SetValue(item, modified);

            Assert.AreEqual(1, raised.Count);
            Assert.AreEqual("GitStatus", raised[0]);
        }

        [TestMethod]
        public void IsActive_WhenToggled_RaisesPropertyChangedForEachChange()
        {
            var item = CreateOpenDocumentItem();
            var notifier = (INotifyPropertyChanged)item;
            var raised = new List<string>();
            notifier.PropertyChanged += (sender, e) => raised.Add(e.PropertyName);

            var property = item.GetType().GetProperty("IsActive", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(property);

            property.SetValue(item, true);
            property.SetValue(item, false);

            Assert.AreEqual(2, raised.Count);
            Assert.AreEqual("IsActive", raised[0]);
            Assert.AreEqual("IsActive", raised[1]);
        }

        private static object CreateOpenDocumentItem()
        {
            var type = VsixTestAssemblyLoader.Load().GetType(OpenDocumentItemTypeName, throwOnError: true);
            return Activator.CreateInstance(type);
        }
    }
}
