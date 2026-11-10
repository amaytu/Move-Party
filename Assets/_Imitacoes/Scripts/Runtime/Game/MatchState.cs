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
        private readonly Random rng = new Random();
        private readonly Queue<string> ruleDeck = new Queue<string>();
        private readonly List<ImitationRecord> records = new List<ImitationRecord>();

        /// <summary>Uma imitação pontuada (para os prêmios do placar final).</summary>
        public sealed class ImitationRecord
        {
            public int Round;
            public PlayerConfig Master;
            public PlayerConfig Imitator;
            public float Score;
            public string Theme;
        }

        /// <summary>Prêmio engraçado do placar final.</summary>
        public readonly struct Award
        {
            public readonly string Title;
            public readonly PlayerConfig Player;
            public readonly string Detail;
            public Award(string title, PlayerConfig player, string detail) { Title = title; Player = player; Detail = detail; }
        }

        /// <summary>Modo extra: cada rodada sorteia também uma regra surpresa.</summary>
        public bool SurpriseMode { get; set; }

        /// <summary>Regra surpresa da rodada atual (null fora do modo).</summary>
        public string CurrentRule { get; private set; }

        /// <summary>Tema + regra, para os títulos ("Imite um robô + Em câmera lenta").</summary>
        public string ThemeLine => CurrentTheme == null ? string.Empty
            : CurrentRule == null ? CurrentTheme.displayText : $"{CurrentTheme.displayText} + {CurrentRule}";

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
            records.Clear();
            ruleDeck.Clear();
            CurrentRule = null;
        }

        // ------------------------------------------------------------------ Rodada

        /// <summary>Começa a rodada atual: sorteia o tema e prepara a vez do Mestre.</summary>
        public ThemeEntry BeginRound()
        {
            CurrentTheme = deck.Draw();
            CurrentRule = SurpriseMode ? DrawRule() : null;
            MasterClip = null;
            IsMasterTurn = true;
            roundScores.Clear();

            // Imitadores: a partir do jogador seguinte ao Mestre, em roda.
            // Ex. com 4: Mestre 2 → imitam 3, 4, 1. Mestre 4 → imitam 1, 2, 3.
            imitators.Clear();
            for (int k = 1; k < players.Count; k++)
                imitators.Add(players[(RoundIndex + k) % players.Count]);
            ImitatorIndex = 0;
            return CurrentTheme;
        }

        /// <summary>O Mestre terminou: passa a vez para o primeiro imitador.</summary>
        public void BeginImitators()
        {
            IsMasterTurn = false;
            ImitatorIndex = 0;
        }

        /// <param name="skipped">Pulado pelo menu de pausa: conta 0, mas não entra nos prêmios.</param>
        public void SetImitatorScore(PlayerConfig player, float score, bool skipped = false)
        {
            roundScores[player.PlayerNumber] = score;
            if (!skipped)
                records.Add(new ImitationRecord { Round = RoundIndex, Master = Master, Imitator = player, Score = score, Theme = CurrentTheme?.displayText });
        }

        /// <summary>Sorteia sem repetir até usar todas as regras.</summary>
        public IReadOnlyList<string> AllRules => SurpriseRules.All;

        private string DrawRule()
        {
            if (ruleDeck.Count == 0)
            {
                var list = new List<string>(SurpriseRules.All);
                for (int i = list.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (list[i], list[j]) = (list[j], list[i]);
                }
                foreach (string r in list) ruleDeck.Enqueue(r);
            }
            return ruleDeck.Dequeue();
        }

        // ------------------------------------------------------------------ Líder (coroa)

        /// <summary>Quem tem o maior total (> 0). Empate no topo = todos com coroa.</summary>
        public bool IsLeader(PlayerConfig player)
        {
            if (player == null || totals.Count == 0) return false;
            float best = totals.Values.Max();
            return best > 0f && GetTotal(player) >= best;
        }

        /// <summary>O líder sozinho no topo, ou null (ninguém pontuou ou empate).</summary>
        public PlayerConfig SoleLeader
        {
            get
            {
                if (totals.Count == 0) return null;
                float best = totals.Values.Max();
                if (best <= 0f) return null;
                var top = players.Where(p => GetTotal(p) >= best).ToList();
                return top.Count == 1 ? top[0] : null;
            }
        }

        // ------------------------------------------------------------------ Prêmios

        public List<Award> GetAwards()
        {
            var awards = new List<Award>();
            if (records.Count == 0) return awards;

            ImitationRecord best = records.OrderByDescending(r => r.Score).First();
            awards.Add(new Award("Mais parecido", best.Imitator, $"{best.Score:0} pontos em \"{best.Theme}\""));

            var cruel = records.GroupBy(r => r.Round)
                .Select(g => new { g.First().Master, Average = g.Average(r => r.Score) })
                .OrderBy(x => x.Average).First();
            if (cruel.Average < 80f)
                awards.Add(new Award("Mestre mais cruel", cruel.Master, $"os imitadores fizeram só {cruel.Average:0} em média"));

            // "Mais criativo" = a pior imitação, só se foi ruim de verdade.
            ImitationRecord worst = records.OrderBy(r => r.Score).First();
            if (records.Count > 1 && worst != best && worst.Score < 60f)
                awards.Add(new Award("Mais criativo", worst.Imitator, $"inventou outra coisa: {worst.Score:0} pontos"));
            return awards;
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
