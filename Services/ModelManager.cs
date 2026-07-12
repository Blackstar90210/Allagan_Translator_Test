using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using LLama.Native;

namespace AllaganTranslator.Services
{
    public class ModelManager
    {
        private readonly IPluginLog log;
        private readonly string configDirectory;
        private readonly string pluginDirectory;
        private const string ModelUrl = "https://huggingface.co/bartowski/Meta-Llama-3.1-8B-Instruct-GGUF/resolve/main/Meta-Llama-3.1-8B-Instruct-Q4_K_M.gguf";

        public event Action<float>? OnDownloadProgress;
        public bool IsDownloading { get; private set; } = false;

        public ModelManager(IPluginLog log, string configDirectory, string pluginDirectory)
        {
            this.log = log;
            this.configDirectory = configDirectory;
            this.pluginDirectory = pluginDirectory;
        }

        public void SetupNativePaths()
        {
            try 
            {
                if (!string.IsNullOrEmpty(pluginDirectory))
                {
                    var nativeDir = Path.Combine(pluginDirectory, "runtimes", "win-x64", "native", "avx2");
                    
                    var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                    if (!currentPath.Contains(nativeDir))
                    {
                        Environment.SetEnvironmentVariable("PATH", nativeDir + ";" + currentPath);
                    }

                    NativeLibraryConfig.All.WithSearchDirectory(nativeDir);
                    NativeLibraryConfig.All.WithLibrary(Path.Combine(nativeDir, "llama.dll"), Path.Combine(nativeDir, "mtmd.dll"));
                }
            }
            catch (Exception ex)
            {
                this.log.Error(ex, "Impossibile impostare la directory nativa per LLamaSharp.");
            }

            NativeLibraryConfig.All.WithLogCallback(delegate(LLamaLogLevel level, string message) {
                // Disable verbose C++ logs
            });
        }

        public async Task<string> EnsureModelDownloadedAsync(CancellationToken token = default)
        {
            var modelPath = Path.Combine(this.configDirectory, "llama_3.1_8b_model.gguf");
            
            if (File.Exists(modelPath) && new FileInfo(modelPath).Length < 4_000_000_000)
            {
                this.log.Information("Rilevato file del modello corrotto o parziale. Eliminazione in corso...");
                File.Delete(modelPath);
            }

            if (!File.Exists(modelPath))
            {
                var tmpPath = modelPath + ".tmp";
                await DownloadModelAsync(tmpPath, token);
                
                if (File.Exists(tmpPath))
                {
                    File.Move(tmpPath, modelPath);
                }
                else
                {
                    throw new Exception("Il download del modello è fallito.");
                }
            }
            return modelPath;
        }

        private async Task DownloadModelAsync(string destPath, CancellationToken token)
        {
            this.IsDownloading = true;
            this.log.Information("[ModelManager] Download del modello linguistico iniziato...");
            
            try
            {
                using var client = new HttpClient();
                using var response = await client.GetAsync(ModelUrl, HttpCompletionOption.ResponseHeadersRead, token);
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength ?? 4920000000L;
                
                using var contentStream = await response.Content.ReadAsStreamAsync(token);
                using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var totalRead = 0L;
                var buffer = new byte[8192];
                var isMoreToRead = true;
                var lastReport = DateTime.Now;

                do
                {
                    var read = await contentStream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (read == 0)
                    {
                        isMoreToRead = false;
                    }
                    else
                    {
                        await fileStream.WriteAsync(buffer, 0, read, token);
                        totalRead += read;

                        if ((DateTime.Now - lastReport).TotalMilliseconds > 500)
                        {
                            var progress = (float)totalRead / totalBytes;
                            OnDownloadProgress?.Invoke(progress);
                            lastReport = DateTime.Now;
                        }
                    }
                }
                while (isMoreToRead);
                
                this.log.Information("[ModelManager] Download completato.");
            }
            catch (OperationCanceledException)
            {
                this.log.Information("[ModelManager] Download annullato.");
                if (File.Exists(destPath)) File.Delete(destPath);
                throw;
            }
            catch (Exception ex)
            {
                this.log.Error(ex, "[ModelManager] Errore durante il download.");
                if (File.Exists(destPath)) File.Delete(destPath);
                throw;
            }
            finally
            {
                this.IsDownloading = false;
            }
        }
    }
}

