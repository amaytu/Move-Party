using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Imitacoes.Editor
{
    /// <summary>
    /// Gera a versão final do jogo (Windows 64 bits, tela cheia, sem informações de debug).
    ///
    /// Menu <b>Imitações ▸ Versão Final</b>:
    /// - <b>Gerar versão final (.exe)</b>: build de release em <c>Builds/Imitacoes/Imitacoes.exe</c>.
    ///   Teclas de debug e o texto de debug ficam desligados (só existem no Editor / Development Build).
    /// - <b>Gerar versão de teste (com debug)</b>: mesma coisa, com as teclas de debug ligadas.
    ///
    /// No PC da apresentação basta copiar a pasta <c>Builds/Imitacoes</c> inteira e ter o
    /// Kinect for Windows Runtime (ou SDK) 1.8 instalado.
    /// </summary>
    public static class FinalBuild
    {
        private const string MenuRoot = "Imitações/Versão Final/";
        private const string ProductName = "Imitações";
        private const string CompanyName = "Move & Party";
        private const string OutputFolder = "Builds/Imitacoes";
        private const string ExeName = "Imitacoes.exe";
        private const string MainScene = "Assets/_Imitacoes/Scenes/Main.unity";

        [MenuItem(MenuRoot + "Gerar versão final (.exe)", priority = 100)]
        private static void BuildRelease() => Build(development: false);

        [MenuItem(MenuRoot + "Gerar versão de teste (com debug)", priority = 101)]
        private static void BuildDevelopment() => Build(development: true);

        [MenuItem(MenuRoot + "Abrir pasta da versão final", priority = 120)]
        private static void OpenFolder()
        {
            string folder = Path.GetFullPath(OutputFolder);
            if (Directory.Exists(folder)) EditorUtility.RevealInFinder(Path.Combine(folder, ExeName));
            else EditorUtility.DisplayDialog("Versão final", "Ainda não existe. Use \"Gerar versão final (.exe)\".", "OK");
        }

        private static void Build(bool development)
        {
            if (!ConfirmKinect()) return;
            ApplyPlayerSettings();

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (!scenes.Contains(MainScene)) scenes = new[] { MainScene }.Concat(scenes).ToArray();

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(OutputFolder, ExeName),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = development ? BuildOptions.Development : BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[Imitações] Versão {(development ? "de teste" : "final")} gerada em {Path.GetFullPath(options.locationPathName)} " +
                          $"({summary.totalSize / (1024f * 1024f):0} MB, {summary.totalTime.TotalSeconds:0} s).");
                EditorUtility.RevealInFinder(Path.GetFullPath(options.locationPathName));
            }
            else
            {
                EditorUtility.DisplayDialog("Versão final",
                    $"A build falhou ({summary.result}). Veja os erros no Console.", "OK");
            }
        }

        /// <summary>Sem o símbolo KINECT_V1 a build sai só com o sensor simulado.</summary>
        private static bool ConfirmKinect()
        {
            string defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone);
            if (defines.Split(';').Contains("KINECT_V1")) return true;

            return EditorUtility.DisplayDialog("Kinect não ativado",
                "O símbolo KINECT_V1 não está ligado (o SDK 1.8 não foi encontrado neste PC).\n\n" +
                "A versão gerada vai funcionar só com o sensor simulado (mouse). Gerar mesmo assim?",
                "Gerar mesmo assim", "Cancelar");
        }

        /// <summary>Tela cheia, nome do jogo e janela fixa: pronto para TV/projetor.</summary>
        private static void ApplyPlayerSettings()
        {
            PlayerSettings.productName = ProductName;
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.resizableWindow = false;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.usePlayerLog = true; // log em %USERPROFILE%\AppData\LocalLow\Move & Party\Imitações
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        }
    }
}
