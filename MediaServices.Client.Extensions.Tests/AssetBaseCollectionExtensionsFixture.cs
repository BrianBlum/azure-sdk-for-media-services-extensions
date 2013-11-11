﻿// <copyright file="AssetBaseCollectionExtensionsFixture.cs" company="Microsoft">Copyright 2013 Microsoft Corporation</copyright>
// <license>
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </license>

namespace MediaServices.Client.Extensions.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.MediaServices.Client;

    [TestClass]
    public class AssetBaseCollectionExtensionsFixture
    {
        private CloudMediaContext context;
        private IAsset asset;

        [TestMethod]
        public void ShouldCreateAssetWithDefaultAccountSelectionStrategy()
        {
            IAccountSelectionStrategy selectionStrategy = RandomAccountSelectionStrategy.FromAccounts(context);
            
            this.asset = this.context.Assets.Create(Guid.NewGuid().ToString(), selectionStrategy, AssetCreationOptions.None);
        }

        [TestMethod]
        public void ShouldRedistributeCreationOfAssetBetweenAllStorageAccounts()
        {
            // Defining list of accounts to select from.
            string[] storageAccountNames = new[] { "account1", "account2", "account3" };

            IAccountSelectionStrategy selectionStrategy = new RandomAccountSelectionStrategy(storageAccountNames);

            var selectedStorageAccounts = new Dictionary<string, int>();
            for (int i = 0; i < 50; i++)
            {
                var selectedStorageAccount = selectionStrategy.SelectAccountForAssets();
                if (!selectedStorageAccounts.ContainsKey(selectedStorageAccount))
                {
                    selectedStorageAccounts.Add(selectedStorageAccount, 0);
                }
                else
                {
                    selectedStorageAccounts[selectedStorageAccount] += 1;
                }

                Thread.Sleep(100);
            }

            // Check if all storage accounts participated in redistribution.
            Assert.AreEqual(storageAccountNames.Length, selectedStorageAccounts.Keys.Count);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ShouldThrowCreatingStrategyIfStorageAccountsArrayIsNull()
        {
            // Defining list of accounts to select from.
            string[] nullStorageAccountNames = null;

            IAccountSelectionStrategy strategy = new RandomAccountSelectionStrategy(nullStorageAccountNames);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void ShouldThrowCreatingStrategyIfStorageAccountsArrayIsEmpty()
        {
            // Defining list of accounts to select from.
            string[] nullStorageAccountNames = new string[0];

            IAccountSelectionStrategy strategy = new RandomAccountSelectionStrategy(nullStorageAccountNames);
        }

        [TestMethod]
        public void ShouldThrowCreateAssetFromFileIfAssetCollectionIsNull()
        {
            AssetBaseCollection nullAssets = null;

            try
            {
                nullAssets.CreateFromFileAsync(string.Empty, AssetCreationOptions.None, CancellationToken.None).Wait();
            }
            catch (AggregateException exception)
            {
                Assert.IsInstanceOfType(exception.InnerException, typeof(ArgumentNullException));
            }
        }

        [TestMethod]
        [DeploymentItem(@"Media\smallwmv1.wmv")]
        public void ShouldCreateAssetFromFile()
        {
            var fileName = "smallwmv1.wmv";
            this.asset = this.context.Assets.CreateFromFile(fileName, null, AssetCreationOptions.None);
            var assetId = this.asset.Id;

            Assert.IsNotNull(this.asset);
            Assert.AreEqual(fileName, this.asset.Name);

            var assetFiles = this.asset.AssetFiles.ToList().OrderBy(a => a.Name);

            Assert.AreEqual(1, assetFiles.Count());
            Assert.AreEqual("smallwmv1.wmv", assetFiles.ElementAt(0).Name);

            this.context = TestHelper.CreateContext();
            Assert.AreEqual(0, this.context.Locators.Where(l => l.AssetId == assetId).Count());
        }

        [TestMethod]
        [DeploymentItem(@"Media\smallwmv1.wmv")]
        public void ShouldCreateAssetFromFileWithDefaultAccountSelectionStrategy()
        {
            RandomAccountSelectionStrategy strategy = RandomAccountSelectionStrategy.FromAccounts(context);

            var fileName = "smallwmv1.wmv";
            this.asset = this.context.Assets.CreateFromFile(fileName, strategy, AssetCreationOptions.None, null);
            var assetId = this.asset.Id;

            Assert.IsNotNull(this.asset);
            Assert.AreEqual(fileName, this.asset.Name);
            IList<string> storageAccountNames = strategy.GetStorageAccounts();
            CollectionAssert.Contains((ICollection)storageAccountNames, this.asset.StorageAccountName);

            var assetFiles = this.asset.AssetFiles.ToList().OrderBy(a => a.Name);

            Assert.AreEqual(1, assetFiles.Count());
            Assert.AreEqual("smallwmv1.wmv", assetFiles.ElementAt(0).Name);

            this.context = TestHelper.CreateContext();
            Assert.AreEqual(0, this.context.Locators.Where(l => l.AssetId == assetId).Count());
        }

        [TestMethod]
        [DeploymentItem(@"Media\smallwmv1.wmv")]
        public void ShouldCreateAssetFromFileWithUploadProgressChangedCallback()
        {
            var uploadResults = new ConcurrentDictionary<string, UploadProgressChangedEventArgs>();
            Action<IAssetFile, UploadProgressChangedEventArgs> uploadProgressChangedCallback =
                (af, e) =>
                {
                    IAssetFile assetFile = af;
                    UploadProgressChangedEventArgs eventArgs = e;

                    Assert.IsNotNull(assetFile);
                    Assert.IsNotNull(eventArgs);

                    uploadResults.AddOrUpdate(assetFile.Name, eventArgs, (k, e2) => eventArgs);
                };

            var fileName = "smallwmv1.wmv";
            this.asset = this.context.Assets.CreateFromFile(fileName, AssetCreationOptions.None, uploadProgressChangedCallback);
            var assetId = this.asset.Id;

            Assert.IsNotNull(this.asset);
            Assert.AreEqual(fileName, this.asset.Name);

            Assert.AreEqual(1, uploadResults.Count);

            AssertUploadedFile(".\\", fileName, uploadResults[fileName]);

            var assetFiles = this.asset.AssetFiles.ToList().OrderBy(a => a.Name);

            Assert.AreEqual(1, assetFiles.Count());
            Assert.AreEqual("smallwmv1.wmv", assetFiles.ElementAt(0).Name);

            this.context = TestHelper.CreateContext();
            Assert.AreEqual(0, this.context.Locators.Where(l => l.AssetId == assetId).Count());
        }

        [TestMethod]
        public void ShouldThrowCreateAssetFromFolderIfAssetCollectionIsNull()
        {
            AssetBaseCollection nullAssets = null;

            try
            {
                nullAssets.CreateFromFolderAsync(string.Empty, null, AssetCreationOptions.None, CancellationToken.None);
            }
            catch (AggregateException exception)
            {
                Assert.IsInstanceOfType(exception.InnerException, typeof(ArgumentNullException));
            }
        }

        [TestMethod]
        public void ShouldThrowCreateAssetFromFolderIfFolderDoesNotContainAnyFiles()
        {
            var emptyFolderName = "EmptyMediaFolder";
            if (Directory.Exists(emptyFolderName))
            {
                Directory.Delete(emptyFolderName, true);
            }

            Directory.CreateDirectory(emptyFolderName);

            try
            {
                this.context.Assets.CreateFromFolderAsync(emptyFolderName, AssetCreationOptions.None, CancellationToken.None);
            }
            catch (AggregateException exception)
            {
                Assert.IsInstanceOfType(exception.InnerException, typeof(FileNotFoundException));
            }
        }

        [TestMethod]
        [DeploymentItem(@"Media\smallwmv1.wmv", "Media")]
        [DeploymentItem(@"Media\smallwmv2.wmv", "Media")]
        [DeploymentItem(@"Media\dummy.ism", "Media")]
        public void ShouldCreateAssetFromFolder()
        {
            var folderName = "Media";
            this.asset = this.context.Assets.CreateFromFolder(folderName, null, AssetCreationOptions.None);
            var assetId = this.asset.Id;

            Assert.IsNotNull(this.asset);
            Assert.AreEqual(folderName, this.asset.Name);

            var assetFiles = this.asset.AssetFiles.ToList().OrderBy(a => a.Name);

            Assert.AreEqual(3, assetFiles.Count());
            Assert.AreEqual("dummy.ism", assetFiles.ElementAt(0).Name);
            Assert.IsTrue(assetFiles.ElementAt(0).IsPrimary);
            Assert.AreEqual("smallwmv1.wmv", assetFiles.ElementAt(1).Name);
            Assert.IsFalse(assetFiles.ElementAt(1).IsPrimary);
            Assert.AreEqual("smallwmv2.wmv", assetFiles.ElementAt(2).Name);
            Assert.IsFalse(assetFiles.ElementAt(2).IsPrimary);

            this.context = TestHelper.CreateContext();
            Assert.AreEqual(0, this.context.Locators.Where(l => l.AssetId == assetId).Count());
        }

        [TestMethod]
        [DeploymentItem(@"Media\smallwmv1.wmv", "Media")]
        [DeploymentItem(@"Media\smallwmv2.wmv", "Media")]
        [DeploymentItem(@"Media\dummy.ism", "Media")]
        public void ShouldCreateAssetFromFolderWithDefaultAccountSelectionStrategy()
        {
            RandomAccountSelectionStrategy strategy = RandomAccountSelectionStrategy.FromAccounts(context);

            var folderName = "Media";
            this.asset = this.context.Assets.CreateFromFolder(folderName, strategy, AssetCreationOptions.None, null);
            var assetId = this.asset.Id;

            Assert.IsNotNull(this.asset);
            Assert.AreEqual(folderName, this.asset.Name);
            IList<string> storageAccountNames = strategy.GetStorageAccounts();
            CollectionAssert.Contains((ICollection)storageAccountNames, this.asset.StorageAccountName);

            var assetFiles = this.asset.AssetFiles.ToList().OrderBy(a => a.Name);

            Assert.AreEqual(3, assetFiles.Count());
            Assert.AreEqual("dummy.ism", assetFiles.ElementAt(0).Name);
            Assert.IsTrue(assetFiles.ElementAt(0).IsPrimary);
            Assert.AreEqual("smallwmv1.wmv", assetFiles.ElementAt(1).Name);
            Assert.IsFalse(assetFiles.ElementAt(1).IsPrimary);
            Assert.AreEqual("smallwmv2.wmv", assetFiles.ElementAt(2).Name);
            Assert.IsFalse(assetFiles.ElementAt(2).IsPrimary);

            this.context = TestHelper.CreateContext();
            Assert.AreEqual(0, this.context.Locators.Where(l => l.AssetId == assetId).Count());
        }

        [TestMethod]
        [DeploymentItem(@"Media\smallwmv1.wmv", "Media")]
        [DeploymentItem(@"Media\smallwmv2.wmv", "Media")]
        [DeploymentItem(@"Media\dummy.ism", "Media")]
        public void ShouldCreateAssetFromFolderWithUploadProgressChangedCallback()
        {
            var uploadResults = new ConcurrentDictionary<string, UploadProgressChangedEventArgs>();
            Action<IAssetFile, UploadProgressChangedEventArgs> uploadProgressChangedCallback =
                (af, e) =>
                {
                    IAssetFile assetFile = af;
                    UploadProgressChangedEventArgs eventArgs = e;

                    Assert.IsNotNull(assetFile);
                    Assert.IsNotNull(eventArgs);

                    uploadResults.AddOrUpdate(assetFile.Name, eventArgs, (k, e2) => eventArgs);
                };

            var folderName = "Media";
            this.asset = this.context.Assets.CreateFromFolder(folderName, AssetCreationOptions.None, uploadProgressChangedCallback);
            var assetId = this.asset.Id;

            Assert.IsNotNull(this.asset);
            Assert.AreEqual(folderName, this.asset.Name);

            Assert.AreEqual(3, uploadResults.Count);

            AssertUploadedFile(folderName, "smallwmv1.wmv", uploadResults["smallwmv1.wmv"]);
            AssertUploadedFile(folderName, "smallwmv2.wmv", uploadResults["smallwmv2.wmv"]);
            AssertUploadedFile(folderName, "dummy.ism", uploadResults["dummy.ism"]);

            var assetFiles = this.asset.AssetFiles.ToList().OrderBy(a => a.Name);

            Assert.AreEqual(3, assetFiles.Count());
            Assert.AreEqual("dummy.ism", assetFiles.ElementAt(0).Name);
            Assert.IsTrue(assetFiles.ElementAt(0).IsPrimary);
            Assert.AreEqual("smallwmv1.wmv", assetFiles.ElementAt(1).Name);
            Assert.IsFalse(assetFiles.ElementAt(1).IsPrimary);
            Assert.AreEqual("smallwmv2.wmv", assetFiles.ElementAt(2).Name);
            Assert.IsFalse(assetFiles.ElementAt(2).IsPrimary);

            this.context = TestHelper.CreateContext();
            Assert.AreEqual(0, this.context.Locators.Where(l => l.AssetId == assetId).Count());
        }

        [TestInitialize]
        public void Initialize()
        {
            this.context = TestHelper.CreateContext();
            this.asset = null;
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (this.asset != null)
            {
                this.asset.Delete();
            }
        }

        private static void AssertUploadedFile(string originalFolderPath, string fileName, UploadProgressChangedEventArgs uploadProgressChangedEventArgs)
        {
            var expected = new FileInfo(Path.Combine(originalFolderPath, fileName));

            Assert.AreEqual(expected.Length, uploadProgressChangedEventArgs.BytesUploaded);
            Assert.AreEqual(expected.Length, uploadProgressChangedEventArgs.TotalBytes);
            Assert.AreEqual(100, uploadProgressChangedEventArgs.Progress);
        }
    }
}
