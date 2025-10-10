//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
//Copyright Warren Harding 2025.
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace MyndSproutApp.Services
{
    /// <summary>
    /// Encapsulates all file I/O and dialogs for opening/saving the Prompt.
    /// Keeps UI/file logic out of the ViewModel.
    /// </summary>
    internal sealed class FilePromptService
    {
        public async Task OpenPromptAsync(MainViewModel vm, Action<string> log)
        {
            if (vm == null) throw new ArgumentNullException(nameof(vm));
            log ??= _ => { };

            var dlg = new OpenFileDialog
            {
                Title = "Open Prompt",
                Filter = "Text files (*.txt;*.prompt)|*.txt;*.prompt|All files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dlg.ShowDialog(Application.Current?.MainWindow) == true)
            {
                string path = dlg.FileName;
                try
                {
                    string text = await File.ReadAllTextAsync(path);
                    vm.Prompt = text;
                    vm.PromptFilePath = path;
                    log($"Opened prompt: {path}");
                }
                catch (Exception ex)
                {
                    log($"OpenPrompt error: {ex.Message}");
                }
            }
            else
            {
                log("OpenPrompt: canceled.");
            }
        }

        public async Task SavePromptAsync(MainViewModel vm, Action<string> log)
        {
            if (vm == null) throw new ArgumentNullException(nameof(vm));
            log ??= _ => { };

            if (!string.IsNullOrWhiteSpace(vm.PromptFilePath))
            {
                await WriteAsync(vm.PromptFilePath!, vm.Prompt, log);
            }
            else
            {
                await SavePromptAsAsync(vm, log);
            }
        }

        public async Task SavePromptAsAsync(MainViewModel vm, Action<string> log)
        {
            if (vm == null) throw new ArgumentNullException(nameof(vm));
            log ??= _ => { };

            var dlg = new SaveFileDialog
            {
                Title = "Save Prompt As",
                Filter = "Text files (*.txt;*.prompt)|*.txt;*.prompt|All files (*.*)|*.*",
                OverwritePrompt = true,
                AddExtension = true,
                DefaultExt = ".txt",
                FileName = string.IsNullOrWhiteSpace(vm.PromptFilePath) ? "prompt.txt" : Path.GetFileName(vm.PromptFilePath)
            };

            if (dlg.ShowDialog(Application.Current?.MainWindow) == true)
            {
                string path = dlg.FileName;
                await WriteAsync(path, vm.Prompt, log);
                vm.PromptFilePath = path;
            }
            else
            {
                log("SavePromptAs: canceled.");
            }
        }

        private static async Task WriteAsync(string path, string content, Action<string> log)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(path, content ?? string.Empty);
                log($"Saved prompt: {path}");
            }
            catch (Exception ex)
            {
                log($"SavePrompt error: {ex.Message}");
            }
        }
    }
}
