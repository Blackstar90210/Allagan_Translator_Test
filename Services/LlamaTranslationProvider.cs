using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using LLama;
using LLama.Common;

namespace AllaganTranslator.Services
{
    public class LlamaTranslationProvider : ITranslationProvider
    {
        private readonly IPluginLog log;
        private readonly ModelManager modelManager;

        private LLamaWeights? model;
        private LLamaContext? context;
        private InstructExecutor? executor;

        public bool IsReady { get; private set; } = false;
        public bool IsDownloading => this.modelManager.IsDownloading;

        public event Action<float>? OnDownloadProgress
        {
            add => this.modelManager.OnDownloadProgress += value;
            remove => this.modelManager.OnDownloadProgress -= value;
        }

        public LlamaTranslationProvider(IPluginLog log, ModelManager modelManager)
        {
            this.log = log;
            this.modelManager = modelManager;
        }

        public async Task InitializeAsync(CancellationToken token = default)
        {
            try
            {
                this.modelManager.SetupNativePaths();
                var modelPath = await this.modelManager.EnsureModelDownloadedAsync(token);

                this.log.Information("Inizializzazione modello Llama in memoria...");
                var parameters = new ModelParams(modelPath)
                {
                    ContextSize = 1024,
                    GpuLayerCount = 0 // CPU by default
                };
                
                this.model = LLamaWeights.LoadFromFile(parameters);
                this.context = this.model.CreateContext(parameters);
                this.executor = new InstructExecutor(this.context);
                
                this.IsReady = true;
                this.log.Information("[LlamaTranslationProvider] Modello caricato e pronto all'uso!");
            }
            catch (Exception ex)
            {
                this.log.Error(ex, "[LlamaTranslationProvider] Errore nel caricamento del modello.");
            }
        }

        public async Task<string> TranslateAsync(string text, string targetLanguage, CancellationToken token)
        {
            if (this.executor == null)
            {
                return "Modello non inizializzato.";
            }
            
            try
            {
                // In un futuro si potrebbe rendere dinamica la lingua target nel prompt
                var prompt = $"<|begin_of_text|><|start_header_id|>system<|end_header_id|>\n\nYou are a professional localization translator for Final Fantasy XIV. Translate the following text into Italian. Output only the Italian translation, nothing else.<|eot_id|><|start_header_id|>user<|end_header_id|>\n\n{text}<|eot_id|><|start_header_id|>assistant<|end_header_id|>\n\n";
                var inferenceParams = new InferenceParams() { MaxTokens = 256, AntiPrompts = new List<string> { "<|eot_id|>" } };

                string translatedText = "";
                await foreach (var tokenText in this.executor.InferAsync(prompt, inferenceParams, token))
                {
                    translatedText += tokenText;
                }
                return translatedText;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                this.log.Error(ex, "Errore durante l'inferenza di Llama.");
                return "Errore locale Llama.";
            }
        }

        public void Dispose()
        {
            this.executor = null;
            this.context?.Dispose();
            this.model?.Dispose();
        }
    }
}

