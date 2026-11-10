using System.Collections.Generic;

namespace Imitacoes.Config
{
    /// <summary>
    /// Modo extra "Regras surpresa": a cada rodada, depois do tema, sorteia um jeito de fazer
    /// o movimento. Vale para o Mestre e para os imitadores.
    /// Nenhuma regra pede para girar ou dar as costas ao sensor (o Kinect perde o esqueleto).
    /// </summary>
    public static class SurpriseRules
    {
        public static readonly IReadOnlyList<string> All = new[]
        {
            "Em câmera lenta",
            "Bem rápido",
            "Só com um braço",
            "Sem mexer as pernas",
            "Agachado",
            "Na ponta dos pés",
            "Com cara de bravo",
            "Morrendo de sono",
            "Como um robô enferrujado",
            "Exagerando tudo",
            "Bem pequenininho",
            "Num pé só",
            "Morrendo de medo",
            "Muito, muito feliz",
            "Como se estivesse no gelo",
            "Como um velhinho",
        };
    }
}
