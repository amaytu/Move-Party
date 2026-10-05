using System;
using System.Collections.Generic;
using System.Linq;
using Imitacoes.Config;
using Imitacoes.Motion;
using Imitacoes.UI;

namespace Imitacoes.Game
{
    /// <summary>
    /// Baralho de temas: sorteia sem repetir na partida. Se todos forem usados
    /// (mais rodadas que temas), embaralha de novo.
    /// </summary>
    public sealed class ThemeDeck
    {
        private readonly List<ThemeEntry> source;
        private readonly Queue<ThemeEntry> queue = new Queue<ThemeEntry>();
        private readonly Random random;

        public ThemeDeck(IEnumerable<ThemeEntry> themes, Random random)
        {
            source = themes.Where(t => t != null && t.enabled).ToList();
            this.random = random;
        }

        public IReadOnlyList<ThemeEntry> All => source;

        public ThemeEntry Draw()
        {
            if (source.Count == 0) return new ThemeEntry { id = "livre", displayText = "Movimento livre!" };
            if (queue.Count == 0) Refill();
            return queue.Dequeue();
        }

        private void Refill()
        {
            // Fisher–Yates.
            var list = new List<ThemeEntry>(source);
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
            foreach (ThemeEntry t in list) queue.Enqueue(t);
        }
    }

    /// <summary>
    /// Estado de UMA partida: jogadores, rodada atual, quem é o Mestre, quem está imitando,
    /// o clipe do Mestre e as pontuações. Não conhece Unity nem UI — fácil de testar.
    /// </summary>
    public sealed class MatchState
    {
        private readonly List<PlayerConfig> players = new List<PlayerConfig>();
        private readonly List<PlayerConfig> imitators = new List<PlayerConfig>();
        private readonly Dictionary<int, float> totals = new Dictionary<int, float>();
        private readonly Dictionary<int, float> roundScores = new Dictionary<int, float>();
        private ThemeDeck deck;

        public IReadOnlyList<PlayerConfig> Players => players;
        public int PlayerCount => players.Count;

        /// <summary>Rodada atual (0-based). Uma rodada por jogador.</summary>
        public int RoundIndex { get; private set; }
        public int RoundCount => players.Count;
        public bool HasNextRound => RoundIndex + 1 < RoundCount;

        public PlayerConfig Master => players[RoundIndex];
        public IReadOnlyList<PlayerConfig> Imitators => imitators;
        public int ImitatorIndex { get; private set; }
        public PlayerConfig CurrentImitator => ImitatorIndex < imitators.Count ? imitators[ImitatorIndex] : null;

        /// <summary>True enquanto o Mestre está se posicionando/gravando.</summary>
        public bool IsMasterTurn { get; private set; } = true;

        /// <summary>Quem está na frente do sensor agora.</summary>
        public PlayerConfig CurrentPerformer => IsMasterTurn ? Master : CurrentImitator;

        public ThemeEntry CurrentTheme { get; private set; }
        public IReadOnlyList<ThemeEntry> AllThemes => deck?.All ?? Array.Empty<ThemeEntry>();
        public MotionClip MasterClip { get; set; }
        public IReadOnlyDictionary<int, float> RoundScores => roundScores;

        // ------------------------------------------------------------------ Partida

        public void StartMatch(IEnumerable<PlayerConfig> matchPlayers, IEnumerable<ThemeEntry> themes, int? seed = null)
        {
            players.Clear();
            players.AddRange(matchPlayers.Where(p => p != null));
            if (players.Count < PlayerRoster.MinPlayers)
                throw new ArgumentException($"A partida precisa de pelo menos {PlayerRoster.MinPlayers} jogadores.");

            totals.Clear();
            foreach (PlayerConfig p in players) totals[p.PlayerNumber] = 0f;

            deck = new ThemeDeck(themes, seed.HasValue ? new Random(seed.Value) : new Random());
            RoundIndex = 0;
        }

        // ------------------------------------------------------------------ Rodada

        /// <summary>Começa a rodada atual: sorteia o tema e prepara a vez do Mestre.</summary>
        public ThemeEntry BeginRound()
        {
            CurrentTheme = deck.Draw();
            MasterClip = null;
            IsMasterTurn = true;
            roundScores.Clear();

            // Imitadores: todos menos o Mestre, em ordem de número.
            imitators.Clear();
            imitators.AddRange(players.Where(p => p != Master));
            ImitatorIndex = 0;
            return CurrentTheme;
        }

        /// <summary>O Mestre terminou: passa a vez para o primeiro imitador.</summary>
        public void BeginImitators()
        {
            IsMasterTurn = false;
            ImitatorIndex = 0;
        }

        public void SetImitatorScore(PlayerConfig player, float score)
        {
            roundScores[player.PlayerNumber] = score;
        }

        /// <summary>Avança para o próximo imitador. False se não há mais.</summary>
        public bool AdvanceImitator()
        {
            ImitatorIndex++;
            return ImitatorIndex < imitators.Count;
        }

        /// <summary>Calcula a pontuação do Mestre e soma a rodada no total.</summary>
        public void FinishRound(MasterScoringRule rule, float multiplier)
        {
            List<float> imitatorScores = imitators
                .Where(p => roundScores.ContainsKey(p.PlayerNumber))
                .Select(p => roundScores[p.PlayerNumber])
                .ToList();

            float masterScore = 0f;
            if (imitatorScores.Count > 0)
            {
                switch (rule)
                {
                    case MasterScoringRule.AverageOfImitators: masterScore = imitatorScores.Average() * multiplier; break;
                    case MasterScoringRule.BestImitator: masterScore = imitatorScores.Max() * multiplier; break;
                    case MasterScoringRule.SumOfImitators: masterScore = imitatorScores.Sum() * multiplier; break;
                }
            }
            roundScores[Master.PlayerNumber] = (float)Math.Round(masterScore);

            foreach (KeyValuePair<int, float> kv in roundScores)
            {
                totals[kv.Key] = (totals.TryGetValue(kv.Key, out float t) ? t : 0f) + kv.Value;
            }
        }

        public void NextRound() => RoundIndex++;

        // ------------------------------------------------------------------ Placar

        public float GetTotal(PlayerConfig player) => totals.TryGetValue(player.PlayerNumber, out float t) ? t : 0f;

        /// <summary>Linhas da rodada, em ordem de jogador, com o Mestre marcado.</summary>
        public List<ScoreRow> GetRoundRows()
        {
            return players.Select(p => new ScoreRow(p,
                roundScores.TryGetValue(p.PlayerNumber, out float s) ? s : 0f,
                p == Master ? "Mestre" : string.Empty)).ToList();
        }

        /// <summary>Ranking final (maior total primeiro; empate = menor número).</summary>
        public List<ScoreRow> GetRanking()
        {
            return players
                .OrderByDescending(GetTotal)
                .ThenBy(p => p.PlayerNumber)
                .Select(p => new ScoreRow(p, GetTotal(p)))
                .ToList();
        }
    }
}
