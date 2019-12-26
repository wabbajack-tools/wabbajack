using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using DynamicData;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.Common;
using Wabbajack.Lib;

namespace Wabbajack.Test
{
    [TestClass]
    public class FilePickerTests
    {
        public static TempFile CreateSetFile(FilePickerVM vm)
        {
            var temp = new TempFile();
            using (new FileStream(temp.File.FullName, FileMode.CreateNew)) { }
            vm.TargetPath = temp.File.FullName;
            return temp;
        }

        public static TempFolder CreateSetFolder(FilePickerVM vm)
        {
            var temp = new TempFolder();
            Directory.CreateDirectory(temp.Dir.FullName);
            vm.TargetPath = temp.Dir.FullName;
            return temp;
        }

        [TestMethod]
        public async Task Stock()
        {
            var vm = new FilePickerVM();
            Assert.AreEqual(FilePickerVM.PathTypeOptions.Off, vm.PathType);
            Assert.AreEqual(FilePickerVM.CheckOptions.Off, vm.ExistCheckOption);
            await Task.Delay(250);
            Assert.IsFalse(vm.Exists);
            Assert.IsTrue(vm.ErrorState.Succeeded);
            Assert.IsFalse(vm.InError);
            Assert.IsTrue(string.IsNullOrEmpty(vm.ErrorTooltip));
        }

        [TestMethod]
        public async Task FileNoExistsCheck_DoesNotExist()
        {
            var vm = new FilePickerVM();
            vm.PathType = FilePickerVM.PathTypeOptions.File;
            vm.ExistCheckOption = FilePickerVM.CheckOptions.Off;
            await Task.Delay(250);
            Assert.IsFalse(vm.Exists);
            Assert.IsTrue(vm.ErrorState.Succeeded);
            Assert.IsFalse(vm.InError);
            Assert.IsTrue(string.IsNullOrEmpty(vm.ErrorTooltip));
        }

        [TestMethod]
        public async Task FileNoExistsCheck_Exists()
        {
            var vm = new FilePickerVM();
            using (CreateSetFile(vm))
            {
                vm.PathType = FilePickerVM.PathTypeOptions.File;
                vm.ExistCheckOption = FilePickerVM.CheckOptions.Off;
                await Task.Delay(250);
                Assert.IsFalse(vm.Exists);
                Assert.IsTrue(vm.ErrorState.Succeeded);
                Assert.IsFalse(vm.InError);
                Assert.IsTrue(string.IsNullOrEmpty(vm.ErrorTooltip));
            }
        }

        [TestMethod]
        public async Task ExistCheckTypeOff_DoesNotExist()
        {
            var vm = new FilePickerVM();
            vm.PathType = FilePickerVM.PathTypeOptions.Off;
            vm.ExistCheckOption = FilePickerVM.CheckOptions.On;
            await Task.Delay(250);
            Assert.IsFalse(vm.Exists);
            Assert.IsTrue(vm.ErrorState.Succeeded);
            Assert.IsFalse(vm.InError);
            Assert.IsTrue(string.IsNullOrEmpty(vm.ErrorTooltip));
        }

        [TestMethod]
        public async Task ExistCheckTypeOff_Exists()
        {
            var vm = new FilePickerVM();
            using (CreateSetFile(vm))
            {
                vm.PathType = FilePickerVM.PathTypeOptions.Off;
                vm.ExistCheckOption = FilePickerVM.CheckOptions.On;
                await Task.Delay(250);
                Assert.IsFalse(vm.Exists);
                Assert.IsTrue(vm.ErrorState.Succeeded);
                Assert.IsFalse(vm.InError);
                Assert.IsTrue(string.IsNullOrEmpty(vm.ErrorTooltip));
            }
        }

        [TestMethod]
        public async Task FileIfNotEmptyCheck_DoesNotExist()
        {
            var vm = new FilePickerVM();
            vm.PathType = FilePickerVM.PathTypeOptions.File;
            vm.ExistCheckOption = FilePickerVM.CheckOptions.IfPathNotEmpty;
            await Task.Delay(250);
            Assert.IsFalse(vm.Exists);
            Assert.IsTrue(vm.ErrorState.Succeeded);
            Assert.IsFalse(vm.InError);
            Assert.IsTrue(string.IsNullOrEmpty(vm.ErrorTooltip));
        }

        [TestMethod]
        public async Task FileIfNotEmptyCheck_SetPath_DoesNotExist()
        {
            var vm = new FilePickerVM();
            vm.PathType = FilePickerVM.PathTypeOptions.File;
            vm.TargetPath = "SomePath.jpg";
            vm.ExistCheckOption = FilePickerVM.CheckOptions.IfPathNotEmpty;
            await Task.Delay(250);
            Assert.IsFalse(vm.Exists);
            Assert.IsFalse(vm.ErrorState.Succeeded);
            Assert.IsTrue(vm.InError);
            Assert.AreEqual(FilePickerVM.PathDoesNotExistText, vm.ErrorTooltip);
        }

        [TestMethod]
        public async Task FileIfNotEmptyCheck_Exists()
        {
            var vm = new FilePickerVM();
            using (CreateSetFile(vm))
            {
                vm.PathType = FilePickerVM.PathTypeOptions.File;
                vm.ExistCheckOption = FilePickerVM.CheckOptions.IfPathNotEmpty;
                await Task.Delay(250);
                Assert.IsTrue(vm.Exists);
                Assert.IsTrue(vm.ErrorState.Succeeded);
                Assert.IsFalse(vm.InError);
                Assert.IsTrue(string.IsNullOrEmpty(vm.ErrorTooltip));
            }
        }

        [TestMethod]
        public async Task FileOnExistsCheck_DoesNotExist()
        {
            var vm = new FilePickerVM();
            vm.PathType = FilePickerVM.PathTypeOptions.File;
            vm.ExistCheckOption = FilePickerVM.CheckOptions.On;
            await Task.Delay(250);
            Assert.IsFalse(vm.Exists);
            Assert.IsFalse(vm.ErrorState.Succeeded);
            Assert.IsTrue(vm.InError);
            Assert.AreEqual(FilePickerVM.PathDoesNotExistText, vm.ErrorTooltip);
        }

        [TestMethod]
        public async Task FileOnExistsCheck_Exists()
        {
            var vm = new FilePickerVM();
            using (CreateSetFile(vm))
            {
                vm.PathType = FilePickerVM.PathTypeOptions.File;
                vm.ExistCheckOption = FilePickerVM.CheckOptions.On;
                await Task.Delay(250);
                Assert.IsTrue(vm.Exists);
                Assert.IsTrue(vm.ErrorState.Succeeded);
                Assert.IsFalse(vm.InError);
                Assert.IsTrue(string.IsNullOrEmpty(vm.ErrorTooltip));
            }
        }

        [TestMethod]
        public async Task FolderIfNotEmptyCheck_DoesNotExist()
        {
            var vm = new FilePickerVM();
            vm.PathType = FilePickerVM.PathTypeOptions.Folder;
            vm.ExistCheckOption = FilePickerVM.CheckOptions.IfPathNotEmpty;
            await Task.Delay(250);
            Assert.IsFalse(vm.Exists);
            Assert.IsTrue(vm.ErrorState.Succeeded);
            Assert.IsFalse(vm.InError);
            Assert.IsTrue(string.IsNullOrEmpty(vm.ErrorTooltip));
        }

        [TestMethod]
        public async Task FolderIfNotEmptyCheck_SetPath_DoesNotExist()
        {
            var vm = new FilePickerVM();
            vm.PathType = FilePickerVM.PathTypeOptions.Folder;
            vm.TargetPath = "SomePath.jpg";
            vm.ExistCheckOption = FilePickerVM.CheckOptions.IfPathNotEmpty;
            await Task.Delay(250);
            Assert.IsFalse(vm.Exists);
            Assert.IsFalse(vm.ErrorState.Succeeded);
            Assert.IsTrue(vm.InError);
            Assert.AreEqual(FilePickerVM.PathDoesNotExistText, vm.ErrorTooltip);
        }

        [TestMethod]
        public async Task FolderIfNotEmptyCheck_Exists()
        {
            var vm = new FilePickerVM();
            using (CreateSetFolder(vm))
            {
                vm.PathType = FilePickerVM.PathTypeOptions.Folder;
                vm.ExistCheckOption = FilePickerVM.CheckOptions.IfPathNotEmpty;
                await Task.Delay(250);
                Assert.IsTrue(vm.Exists);
                Assert.IsTrue(vm.ErrorState.Succeeded);
                Assert.IsFalse(vm.InError);
                Assert.IsTrue(string.IsNullOrEmpty(vm.ErrorTooltip));
            }
        }

        [TestMethod]
        public async Task FolderOnExistsCheck_DoesNotExist()
        {
            var vm = new FilePickerVM();
            vm.PathType = FilePickerVM.PathTypeOptions.Folder;
            vm.ExistCheckOption = FilePickerVM.CheckOptions.On;
            await Task.Delay(250);
            Assert.IsFalse(vm.Exists);
            Assert.IsFalse(vm.ErrorState.Succeeded);
            Assert.IsTrue(vm.InError);
            Assert.AreEqual(FilePickerVM.PathDoesNotExistText, vm.ErrorTooltip);
        }

        [TestMethod]
        public async Task FolderOnExistsCheck_Exists()
        {
            var vm = new FilePickerVM();
            using (CreateSetFolder(vm))
            {
                vm.PathType = FilePickerVM.PathTypeOptions.Folder;
                vm.ExistCheckOption = FilePickerVM.CheckOptions.On;
                await Task.Delay(250);
                Assert.IsTrue(vm.Exists);
                Assert.IsTrue(vm.ErrorState.Succeeded);
                Assert.IsFalse(vm.InError);
                Assert.IsTrue(string.IsNullOrEmpty(vm.ErrorTooltip));
            }
        }

        [TestMethod]
        public async Task AdditionalError_Success()
        {
            var vm = new FilePickerVM();
            vm.AdditionalError = Observable.Return<IErrorResponse>(ErrorResponse.Succeed());
            await Task.Delay(250);
            Assert.IsTrue(vm.ErrorState.Succeeded);
            Assert.IsFalse(vm.InError);
            Assert.IsTrue(string.IsNullOrEmpty(vm.ErrorTooltip));
        }

        [TestMethod]
        public async Task AdditionalError_Fail()
        {
            var vm = new FilePickerVM();
            string errText = "An error";
            vm.AdditionalError = Observable.Return<IErrorResponse>(ErrorResponse.Fail(errText));
            await Task.Delay(250);
            Assert.IsFalse(vm.ErrorState.Succeeded);
            Assert.IsTrue(vm.InError);
            Assert.AreEqual(errText, vm.ErrorTooltip);
        }

        [TestMethod]
        public async Task FileExistsButSetToFolder()
        {
            var vm = new FilePickerVM();
            using (CreateSetFile(vm))
            {
                vm.PathType = FilePickerVM.PathTypeOptions.Folder;
                vm.ExistCheckOption = FilePickerVM.CheckOptions.On;
                await Task.Delay(250);
                Assert.IsFalse(vm.Exists);
                Assert.IsFalse(vm.ErrorState.Succeeded);
                Assert.IsTrue(vm.InError);
                Assert.AreEqual(FilePickerVM.PathDoesNotExistText, vm.ErrorTooltip);
            }
        }

        [TestMethod]
        public async Task FolderExistsButSetToFile()
        {
            var vm = new FilePickerVM();
            using (CreateSetFolder(vm))
            {
                vm.PathType = FilePickerVM.PathTypeOptions.File;
                vm.ExistCheckOption = FilePickerVM.CheckOptions.On;
                await Task.Delay(250);
                Assert.IsFalse(vm.Exists);
                Assert.IsFalse(vm.ErrorState.Succeeded);
                Assert.IsTrue(vm.InError);
                Assert.AreEqual(FilePickerVM.PathDoesNotExistText, vm.ErrorTooltip);
            }
        }

        [TestMethod]
        public async Task FileWithFilters_Passes()
        {
            var vm = new FilePickerVM();
            using (CreateSetFile(vm))
            {
                vm.PathType = FilePickerVM.PathTypeOptions.File;
                vm.ExistCheckOption = FilePickerVM.CheckOptions.Off;
                vm.Filters.Add(new FilePickerVM.CommonFileDialogFilter("test", $"*.{Path.GetExtension(vm.TargetPath)}"));
                await Task.Delay(250);
                Assert.IsFalse(vm.Exists);
                Assert.IsTrue(vm.ErrorState.Succeeded);
                Assert.IsFalse(vm.InError);
                Assert.IsTrue(string.IsNullOrEmpty(vm.ErrorTooltip));
            }
        }

        [TestMethod]
        public async Task FileWithFilters_ExistsButFails()
        {
            var vm = new FilePickerVM();
            using (CreateSetFile(vm))
            {
                vm.PathType = FilePickerVM.PathTypeOptions.File;
                vm.ExistCheckOption = FilePickerVM.CheckOptions.Off;
                vm.Filters.Add(new FilePickerVM.CommonFileDialogFilter("test", $"*.{Path.GetExtension(vm.TargetPath)}z"));
                await Task.Delay(250);
                Assert.IsFalse(vm.Exists);
                Assert.IsFalse(vm.ErrorState.Succeeded);
                Assert.IsTrue(vm.InError);
                Assert.AreEqual(FilePickerVM.DoesNotPassFiltersText, vm.ErrorTooltip);
            }
        }

        [TestMethod]
        public async Task FileWithFilters_PassesButDoesntExist()
        {
            var vm = new FilePickerVM();
            vm.PathType = FilePickerVM.PathTypeOptions.File;
            vm.ExistCheckOption = FilePickerVM.CheckOptions.Off;
            vm.TargetPath = "SomePath.png";
            vm.Filters.Add(new FilePickerVM.CommonFileDialogFilter("test", $"*.{Path.GetExtension(vm.TargetPath)}"));
            await Task.Delay(250);
            Assert.IsFalse(vm.Exists);
            Assert.IsTrue(vm.ErrorState.Succeeded);
            Assert.IsFalse(vm.InError);
            Assert.IsTrue(string.IsNullOrEmpty(vm.ErrorTooltip));
        }

        [TestMethod]
        public async Task FileWithFilters_IfNotEmptyCheck_DoesntExist()
        {
            var vm = new FilePickerVM();
            vm.PathType = FilePickerVM.PathTypeOptions.File;
            vm.ExistCheckOption = FilePickerVM.CheckOptions.Off;
            vm.FilterCheckOption = FilePickerVM.CheckOptions.IfPathNotEmpty;
            vm.Filters.Add(new FilePickerVM.CommonFileDialogFilter("test", $"*.{Path.GetExtension(vm.TargetPath)}"));
            await Task.Delay(250);
            Assert.IsFalse(vm.Exists);
            Assert.IsTrue(vm.ErrorState.Succeeded);
            Assert.IsFalse(vm.InError);
            Assert.IsTrue(string.IsNullOrEmpty(vm.ErrorTooltip));
        }
    }
}
