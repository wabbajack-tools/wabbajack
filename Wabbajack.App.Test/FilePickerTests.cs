using System.IO;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using Wabbajack.Common;
using Xunit;

namespace Wabbajack.Test
{
    public class FilePickerTests
    {
        public static TempFile CreateSetFile(FilePickerVM vm)
        {
            var temp = new TempFile();
            using (new FileStream(temp.File.FullName, FileMode.CreateNew)) { }
            vm.TargetPath = temp.Path;
            return temp;
        }

        [Fact]
        public async Task Stock()
        {
            var vm = new FilePickerVM();
            Assert.Equal(FilePickerVM.PathTypeOptions.Off, vm.PathType);
            Assert.Equal(FilePickerVM.CheckOptions.Off, vm.ExistCheckOption);
            await Task.Delay(250);
            Assert.False(vm.Exists);
            Assert.True(vm.ErrorState.Succeeded);
            Assert.False(vm.InError);
            Assert.True(string.IsNullOrEmpty(vm.ErrorTooltip));
        }

        [Fact]
        public async Task FileNoExistsCheck_DoesNotExist()
        {
            var vm = new FilePickerVM();
            vm.PathType = FilePickerVM.PathTypeOptions.File;
            vm.ExistCheckOption = FilePickerVM.CheckOptions.Off;
            await Task.Delay(250);
            Assert.False(vm.Exists);
            Assert.True(vm.ErrorState.Succeeded, vm.ErrorState.Reason);
            Assert.False(vm.InError);
            Assert.True(string.IsNullOrEmpty(vm.ErrorTooltip));
        }

        [Fact]
        public async Task FileNoExistsCheck_Exists()
        {
            var vm = new FilePickerVM();
            using (CreateSetFile(vm))
            {
                vm.PathType = FilePickerVM.PathTypeOptions.File;
                vm.ExistCheckOption = FilePickerVM.CheckOptions.Off;
                await Task.Delay(250);
                Assert.False(vm.Exists);
                Assert.True(vm.ErrorState.Succeeded);
                Assert.False(vm.InError);
                Assert.True(string.IsNullOrEmpty(vm.ErrorTooltip));
            }
        }

        [Fact]
        public async Task ExistCheckTypeOff_DoesNotExist()
        {
            var vm = new FilePickerVM();
            vm.PathType = FilePickerVM.PathTypeOptions.Off;
            vm.ExistCheckOption = FilePickerVM.CheckOptions.On;
            await Task.Delay(250);
            Assert.False(vm.Exists);
            Assert.True(vm.ErrorState.Succeeded);
            Assert.False(vm.InError);
            Assert.True(string.IsNullOrEmpty(vm.ErrorTooltip));
        }

        [Fact]
        public async Task ExistCheckTypeOff_Exists()
        {
            var vm = new FilePickerVM();
            using (CreateSetFile(vm))
            {
                vm.PathType = FilePickerVM.PathTypeOptions.Off;
                vm.ExistCheckOption = FilePickerVM.CheckOptions.On;
                await Task.Delay(250);
                Assert.False(vm.Exists);
                Assert.True(vm.ErrorState.Succeeded);
                Assert.False(vm.InError);
                Assert.True(string.IsNullOrEmpty(vm.ErrorTooltip));
            }
        }

        [Fact]
        public async Task FileIfNotEmptyCheck_DoesNotExist()
        {
            var vm = new FilePickerVM();
            vm.PathType = FilePickerVM.PathTypeOptions.File;
            vm.ExistCheckOption = FilePickerVM.CheckOptions.IfPathNotEmpty;
            await Task.Delay(250);
            Assert.False(vm.Exists);
            Assert.True(vm.ErrorState.Succeeded);
            Assert.False(vm.InError);
            Assert.True(string.IsNullOrEmpty(vm.ErrorTooltip));
        }

        [Fact]
        public async Task FileIfNotEmptyCheck_SetPath_DoesNotExist()
        {
            var vm = new FilePickerVM();
            vm.PathType = FilePickerVM.PathTypeOptions.File;
            vm.TargetPath = (AbsolutePath)"SomePath.jpg";
            vm.ExistCheckOption = FilePickerVM.CheckOptions.IfPathNotEmpty;
            await Task.Delay(250);
            Assert.False(vm.Exists);
            Assert.False(vm.ErrorState.Succeeded);
            Assert.True(vm.InError);
            Assert.Equal(FilePickerVM.PathDoesNotExistText, vm.ErrorTooltip);
        }

        [Fact]
        public async Task FileIfNotEmptyCheck_Exists()
        {
            var vm = new FilePickerVM();
            using (CreateSetFile(vm))
            {
                vm.PathType = FilePickerVM.PathTypeOptions.File;
                vm.ExistCheckOption = FilePickerVM.CheckOptions.IfPathNotEmpty;
                await Task.Delay(250);
                Assert.True(vm.Exists);
                Assert.True(vm.ErrorState.Succeeded);
                Assert.False(vm.InError);
                Assert.True(string.IsNullOrEmpty(vm.ErrorTooltip));
            }
        }

        [Fact]
        public async Task FileOnExistsCheck_DoesNotExist()
        {
            var vm = new FilePickerVM();
            vm.PathType = FilePickerVM.PathTypeOptions.File;
            vm.ExistCheckOption = FilePickerVM.CheckOptions.On;
            await Task.Delay(250);
            Assert.False(vm.Exists);
            Assert.False(vm.ErrorState.Succeeded);
            Assert.True(vm.InError);
            Assert.Equal(FilePickerVM.PathDoesNotExistText, vm.ErrorTooltip);
        }

        [Fact]
        public async Task FileOnExistsCheck_Exists()
        {
            var vm = new FilePickerVM();
            using (CreateSetFile(vm))
            {
                vm.PathType = FilePickerVM.PathTypeOptions.File;
                vm.ExistCheckOption = FilePickerVM.CheckOptions.On;
                await Task.Delay(250);
                Assert.True(vm.Exists);
                Assert.True(vm.ErrorState.Succeeded);
                Assert.False(vm.InError);
                Assert.True(string.IsNullOrEmpty(vm.ErrorTooltip));
            }
        }

        [Fact]
        public async Task FolderIfNotEmptyCheck_DoesNotExist()
        {
            var vm = new FilePickerVM();
            vm.PathType = FilePickerVM.PathTypeOptions.Folder;
            vm.ExistCheckOption = FilePickerVM.CheckOptions.IfPathNotEmpty;
            await Task.Delay(250);
            Assert.False(vm.Exists);
            Assert.True(vm.ErrorState.Succeeded, vm.ErrorState.Reason);
            Assert.False(vm.InError);
            Assert.True(string.IsNullOrEmpty(vm.ErrorTooltip));
        }

        [Fact]
        public async Task FolderIfNotEmptyCheck_SetPath_DoesNotExist()
        {
            var vm = new FilePickerVM();
            vm.PathType = FilePickerVM.PathTypeOptions.Folder;
            vm.TargetPath = (AbsolutePath)"SomePath.jpg";
            vm.ExistCheckOption = FilePickerVM.CheckOptions.IfPathNotEmpty;
            await Task.Delay(250);
            Assert.False(vm.Exists);
            Assert.False(vm.ErrorState.Succeeded, vm.ErrorState.Reason);
            Assert.True(vm.InError);
            Assert.Equal(FilePickerVM.PathDoesNotExistText, vm.ErrorTooltip);
        }

        [Fact]
        public async Task FolderIfNotEmptyCheck_Exists()
        {
            var vm = new FilePickerVM();
            await using (await CreateSetFolder(vm))
            {
                vm.PathType = FilePickerVM.PathTypeOptions.Folder;
                vm.ExistCheckOption = FilePickerVM.CheckOptions.IfPathNotEmpty;
                await Task.Delay(250);
                Assert.True(vm.Exists);
                Assert.True(vm.ErrorState.Succeeded);
                Assert.False(vm.InError);
                Assert.True(string.IsNullOrEmpty(vm.ErrorTooltip));
            }
        }

        [Fact]
        public async Task FolderOnExistsCheck_DoesNotExist()
        {
            var vm = new FilePickerVM();
            vm.PathType = FilePickerVM.PathTypeOptions.Folder;
            vm.ExistCheckOption = FilePickerVM.CheckOptions.On;
            await Task.Delay(250);
            Assert.False(vm.Exists);
            Assert.False(vm.ErrorState.Succeeded);
            Assert.True(vm.InError);
            Assert.Equal(FilePickerVM.PathDoesNotExistText, vm.ErrorTooltip);
        }

        [Fact]
        public async Task FolderOnExistsCheck_Exists()
        {
            var vm = new FilePickerVM();
            await using (await CreateSetFolder(vm))
            {
                vm.PathType = FilePickerVM.PathTypeOptions.Folder;
                vm.ExistCheckOption = FilePickerVM.CheckOptions.On;
                await Task.Delay(250);
                Assert.True(vm.Exists);
                Assert.True(vm.ErrorState.Succeeded);
                Assert.False(vm.InError);
                Assert.True(string.IsNullOrEmpty(vm.ErrorTooltip));
            }
        }

        [Fact]
        public async Task AdditionalError_Success()
        {
            var vm = new FilePickerVM();
            vm.AdditionalError = Observable.Return<IErrorResponse>(ErrorResponse.Succeed());
            await Task.Delay(250);
            Assert.True(vm.ErrorState.Succeeded);
            Assert.False(vm.InError);
            Assert.True(string.IsNullOrEmpty(vm.ErrorTooltip));
        }

        [Fact]
        public async Task AdditionalError_Fail()
        {
            var vm = new FilePickerVM();
            string errText = "An error";
            vm.AdditionalError = Observable.Return<IErrorResponse>(ErrorResponse.Fail(errText));
            await Task.Delay(250);
            Assert.False(vm.ErrorState.Succeeded);
            Assert.True(vm.InError);
            Assert.Equal(errText, vm.ErrorTooltip);
        }

        [Fact]
        public async Task FileExistsButSetToFolder()
        {
            var vm = new FilePickerVM();
            using (CreateSetFile(vm))
            {
                vm.PathType = FilePickerVM.PathTypeOptions.Folder;
                vm.ExistCheckOption = FilePickerVM.CheckOptions.On;
                await Task.Delay(250);
                Assert.False(vm.Exists);
                Assert.False(vm.ErrorState.Succeeded);
                Assert.True(vm.InError);
                Assert.Equal(FilePickerVM.PathDoesNotExistText, vm.ErrorTooltip);
            }
        }

        [Fact]
        public async Task FolderExistsButSetToFile()
        {
            var vm = new FilePickerVM();
            await using (await CreateSetFolder(vm))
            {
                vm.PathType = FilePickerVM.PathTypeOptions.File;
                vm.ExistCheckOption = FilePickerVM.CheckOptions.On;
                await Task.Delay(250);
                Assert.False(vm.Exists);
                Assert.False(vm.ErrorState.Succeeded);
                Assert.True(vm.InError);
                Assert.Equal(FilePickerVM.PathDoesNotExistText, vm.ErrorTooltip);
            }
        }

        [Fact]
        public async Task FileWithFilters_Passes()
        {
            var vm = new FilePickerVM();
            using (CreateSetFile(vm))
            {
                vm.PathType = FilePickerVM.PathTypeOptions.File;
                vm.ExistCheckOption = FilePickerVM.CheckOptions.Off;
                vm.Filters.Add(new Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogFilter("test", $"*.{vm.TargetPath.Extension}"));
                await Task.Delay(250);
                Assert.False(vm.Exists);
                Assert.True(vm.ErrorState.Succeeded);
                Assert.False(vm.InError);
                Assert.True(string.IsNullOrEmpty(vm.ErrorTooltip));
            }
        }

        [Fact]
        public async Task FileWithFilters_ExistsButFails()
        {
            var vm = new FilePickerVM();
            using (CreateSetFile(vm))
            {
                vm.PathType = FilePickerVM.PathTypeOptions.File;
                vm.ExistCheckOption = FilePickerVM.CheckOptions.Off;
                vm.Filters.Add(new Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogFilter("test", $"*.{vm.TargetPath.Extension}z"));
                await Task.Delay(250);
                Assert.False(vm.Exists);
                Assert.False(vm.ErrorState.Succeeded);
                Assert.True(vm.InError);
                Assert.Equal(FilePickerVM.DoesNotPassFiltersText, vm.ErrorTooltip);
            }
        }

        [Fact]
        public async Task FileWithFilters_PassesButDoesntExist()
        {
            var vm = new FilePickerVM();
            vm.PathType = FilePickerVM.PathTypeOptions.File;
            vm.ExistCheckOption = FilePickerVM.CheckOptions.Off;
            vm.TargetPath = (AbsolutePath)"SomePath.png";
            vm.Filters.Add(new Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogFilter("test", $"*.{vm.TargetPath.Extension}"));
            await Task.Delay(250);
            Assert.False(vm.Exists);
            Assert.True(vm.ErrorState.Succeeded, vm.ErrorState.Reason);
            Assert.False(vm.InError);
            Assert.True(string.IsNullOrEmpty(vm.ErrorTooltip));
        }

        [Fact]
        public async Task FileWithFilters_IfNotEmptyCheck_DoesntExist()
        {
            var vm = new FilePickerVM
            {
                PathType = FilePickerVM.PathTypeOptions.File,
                ExistCheckOption = FilePickerVM.CheckOptions.Off,
                FilterCheckOption = FilePickerVM.CheckOptions.IfPathNotEmpty
            };
            vm.Filters.Add(new Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogFilter("test", $"*.{vm.TargetPath.Extension}"));
            await Task.Delay(250);
            Assert.False(vm.Exists);
            Assert.True(vm.ErrorState.Succeeded);
            Assert.False(vm.InError);
            Assert.True(string.IsNullOrEmpty(vm.ErrorTooltip));
        }

        private static async Task<TempFolder> CreateSetFolder(FilePickerVM vm)
        {
            var temp = await TempFolder.Create();
            vm.TargetPath = temp.Dir;
            return temp;
        }
    }
}
