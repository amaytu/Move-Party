using System;
using System.Collections.Generic;
using UnityEngine;

namespace Imitacoes.Config
{
    /// <summary>Um tema sorteável ("Imite uma galinha").</summary>
    [Serializable]
    public sealed class ThemeEntry
    {
        [Tooltip("Identificador estável (sem espaços). Vai para os arquivos de gravação.")]
        public string id = string.Empty;

        [Tooltip("Texto exibido na roleta.")]
        public string displayText = "Imite ...";

        [Tooltip("Ícone opcional exibido junto do texto.")]
        public Sprite icon;

        [Tooltip("Desmarque para tirar do sorteio sem apagar.")]
        public bool enabled = true;
    }

    /// <summary>
    /// Lista configurável de temas. O sorteio sem repetição na partida é feito em tempo de
    /// execução (baralho embaralhado), sem alterar este asset.
    /// </summary>
    [CreateAssetMenu(fileName = "ThemeCatalog", menuName = "Imitações/Config/Catálogo de Temas", order = 2)]
    public sealed class ThemeCatalog : ScriptableObject
    {
        [SerializeField] private List<ThemeEntry> themes = new List<ThemeEntry>();

        public IReadOnlyList<ThemeEntry> Themes => themes;

        /// <summary>Temas marcados como ativos (aloca uma lista nova).</summary>
        public List<ThemeEntry> GetEnabledThemes() => themes.FindAll(t => t != null && t.enabled);

        // Chamado pela Unity ao criar o asset ou em "Reset" no Inspector: já vem com exemplos.
        private void Reset()
        {
            themes = new List<ThemeEntry>
            {
                New("galinha", "Imite uma galinha"),
                New("robo", "Imite um robô"),
                New("macaco", "Imite um macaco"),
                New("pinguim", "Imite um pinguim"),
                New("super-heroi", "Imite um super-herói voando"),
                New("goleiro", "Imite um goleiro defendendo"),
                New("bailarina", "Imite uma bailarina"),
                New("zumbi", "Imite um zumbi"),
                New("guitarrista", "Imite um guitarrista de rock"),
                New("elefante", "Imite um elefante"),
            };
        }

        private void OnValidate()
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < themes.Count; i++)
            {
                ThemeEntry t = themes[i];
                if (t == null) continue;
                if (string.IsNullOrWhiteSpace(t.id)) t.id = $"tema-{i + 1}";
                if (!seen.Add(t.id)) Debug.LogWarning($"[{name}] Id de tema repetido: '{t.id}'.", this);
            }
        }

        private static ThemeEntry New(string id, string text) => new ThemeEntry { id = id, displayText = text };
    }
}
