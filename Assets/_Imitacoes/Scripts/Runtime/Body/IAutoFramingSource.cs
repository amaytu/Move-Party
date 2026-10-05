namespace Imitacoes.Body
{
    /// <summary>
    /// Fonte capaz de reenquadrar o jogador sozinha (ex.: motor de inclinação do Kinect v1).
    /// O jogo liga isso só fora das gravações, para o movimento do sensor não virar movimento do jogador.
    /// </summary>
    public interface IAutoFramingSource
    {
        bool AutoFramingAllowed { get; set; }
    }
}
