using System.Collections.Generic;
using UnityEngine;

namespace Imitacoes.Config
{
    /// <summary>
    /// Lista ordenada dos 4 <see cref="PlayerConfig"/> possíveis.
    /// Ao escolher "3 jogadores", o jogo usa os 3 primeiros desta lista.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerRoster", menuName = "Imitações/Config/Elenco de Jogadores", order = 1)]
    public sealed class PlayerRoster : ScriptableObject
    {
        public const int MinPlayers = 2;
        public const int MaxPlayers = 4;

        [SerializeField, Tooltip("Jogador 1 (Vermelho), 2 (Verde), 3 (Amarelo), 4 (Azul), nesta ordem.")]
        private PlayerConfig[] players = new PlayerConfig[MaxPlayers];

        public IReadOnlyList<PlayerConfig> Players => players;

        public int Count => players?.Length ?? 0;

        /// <summary>Retorna o jogador pelo número (1..4) ou null.</summary>
        public PlayerConfig GetByNumber(int playerNumber)
        {
            if (players == null) return null;
            foreach (PlayerConfig p in players)
            {
                if (p != null && p.PlayerNumber == playerNumber) return p;
            }
            return null;
        }

        /// <summary>Os primeiros <paramref name="count"/> jogadores (limitado a 2..4).</summary>
        public List<PlayerConfig> Take(int count)
        {
            count = Mathf.Clamp(count, MinPlayers, Mathf.Min(MaxPlayers, Count));
            var result = new List<PlayerConfig>(count);
            for (int i = 0; i < count; i++) result.Add(players[i]);
            return result;
        }

        private void OnValidate()
        {
            if (players == null) return;

            var seen = new HashSet<int>();
            for (int i = 0; i < players.Length; i++)
            {
                PlayerConfig p = players[i];
                if (p == null)
                {
                    Debug.LogWarning($"[{name}] Posição {i} do elenco está vazia.", this);
                    continue;
                }
                if (!seen.Add(p.PlayerNumber))
                    Debug.LogWarning($"[{name}] Número de jogador repetido: {p.PlayerNumber}.", this);
                if (p.PlayerNumber != i + 1)
                    Debug.LogWarning($"[{name}] Posição {i} contém o Jogador {p.PlayerNumber}; esperado {i + 1}.", this);
            }
        }
    }
}
