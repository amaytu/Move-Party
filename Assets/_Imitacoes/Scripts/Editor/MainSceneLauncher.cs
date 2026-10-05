using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Imitacoes.Editor
{
    /// <summary>
    /// Garante que o Play do Editor sempre comece pela cena principal do jogo
    /// (<c>Assets/_Imitacoes/Scenes/Main.unity</c>), mesmo que outra cena esteja aberta.
    ///
    /// Por que existe: a cena aberta fica salva em Library/UserSettings, que não vão para o Git.
    /// Num clone novo a Unity abre a SampleScene do template e o Play rodava a cena errada.
    ///
    /// - Ao abrir o projeto: se a cena aberta for a SampleScene ou uma cena vazia, abre a Main.
    /// - Play: usa <see cref="EditorSceneManager.playModeStartScene"/> = Main.
    ///   Desligue em <b>Imitações ▸ Play sempre na cena principal</b> para testar outras cenas.
    /// </summary>
    [InitializeOnLoad]
    public static class MainSceneLauncher
    {
        private const string MainScenePath = "Assets/_Imitacoes/Scenes/Main.unity";
        private const string MenuPath = "Imitações/Play sempre na cena principal";
        private const string PrefKey = "Imitacoes.PlayFromMainScene";

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        static MainSceneLauncher()
        {
            // Espera o Editor terminar de carregar antes de mexer em cenas.
            EditorApplication.delayCall += Apply;
        }

        private static void Apply()
        {
            SceneAsset main = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath);
            EditorSceneManager.playModeStartScene = Enabled ? main : null;

            if (main == null || EditorApplication.isPlayingOrWillChangePlaymode) return;

            // Clone novo: troca a cena do template (ou cena vazia) pela principal.
            Scene active = SceneManager.GetActiveScene();
            bool isTemplateScene = string.IsNullOrEmpty(active.path) || active.path.EndsWith("/SampleScene.unity");
            if (isTemplateScene && !active.isDirty && SceneManager.sceneCount == 1)
                EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        }

        [MenuItem(MenuPath, priority = 50)]
        private static void Toggle()
        {
            Enabled = !Enabled;
            Apply();
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }
    }
}
