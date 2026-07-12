## [1.0.0.17] - Refactoring Modelli & Download
- Rimosso completamente il supporto GPU/Vulkan a causa di instabilità con Dalamud
- Declassato il modello Llama 3.1 8B a esecuzione esclusivamente CPU (AVX2)
- Implementato un sistema di avvisi esplicito sul peso computazionale del modello 8B
- Aggiunto il download manuale del modello: i file non verranno più scaricati in background all'avvio, ma richiederanno l'azione esplicita dell'utente
- Corretto bug logico che impediva il corretto caricamento di un nuovo modello se ne era già stato caricato uno in precedenza
- Ottimizzate le dimensioni del file `.zip` (librerie Vulkan scartate)
- Ottimizzazione prestazionale: StringBuilder per l'inferenza e Regex pre-compilate con caching

* **[Feature]** Mantenuto il modello leggero **Llama 3.2 3B** (su CPU) per computer meno performanti.
* **[Miglioramento]** Nuove opzioni nel menù per lo switch a caldo tra IA Cloud e IA Locale CPU (3B o 8B).
* **[Miglioramento]** Parametri di generazione ("Temperature" e "Top-P") ottimizzati per impedire all'IA di "inventare" frasi non presenti nell'originale.
