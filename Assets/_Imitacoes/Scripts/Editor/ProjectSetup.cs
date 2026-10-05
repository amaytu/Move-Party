using System;
using System.IO;
using System.Linq;
using Imitacoes.Config;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Imitacoes.EditorTools
{
    /// <summary>
    /// Configura o projeto automaticamente ao abrir a Unity (e pelo menu Imitações).
    ///
    /// 1. Player Settings (uma vez por projeto): Mono, .NET Standard 2.1, 1920×1080 tela cheia,
    ///    rodar em segundo plano, alvo Windows x86_64.
    /// 2. Assets de configuração (se não existirem): Jogador1..4, PlayerRoster, ThemeCatalog, GameSettings.
    /// 3. Kinect v1 (sempre que abrir): liga o símbolo KINECT_V1 se o SDK 1.8 estiver instalado
    ///    (Kinect10.dll no System32) e desliga caso contrário.
    /// </summary>
    [InitializeOnLoad]
    internal static class ProjectSetup
    {
        private const string MenuRoot = "Imitações/";
        private const string DataRoot = "Assets/_Imitacoes/Data";
        private const string KinectDefine = "KINECT_V1";

        // Chave por projeto: o mesmo PC pode ter outros projetos.
        private static string PlayerSettingsKey => "Imitacoes.PlayerSettingsApplied.v1." + Application.dataPath.GetHashCode();

        static ProjectSetup()
        {
            EditorApplication.delayCall += WaitUntilIdle;
        }

        // Espera a Unity terminar importação/compilação para não brigar com ela.
        private static void WaitUntilIdle()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += WaitUntilIdle;
                return;
            }

            try
            {
                if (!EditorPrefs.GetBool(PlayerSettingsKey, false))
                {
                    ApplyPlayerSettings();
                    EditorPrefs.SetBool(PlayerSettingsKey, true);
                }

                CreateConfigAssetsIfMissing();
                SetupKinect();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        // ------------------------------------------------------------------ Menu

        [MenuItem(MenuRoot + "Configurar Projeto (reaplicar tudo)", priority = 0)]
        private static void RunAllFromMenu()
        {
            ApplyPlayerSettings();
            CreateConfigAssetsIfMissing();
            SetupKinect();
            Debug.Log("[Imitações] Configuração reaplicada.");
        }

        [MenuItem(MenuRoot + "Kinect/Verificar instalação", priority = 20)]
        private static void CheckKinectFromMenu()
        {
            SetupKinect();
            bool runtime = IsKinectV1RuntimeInstalled();
            EditorUtility.DisplayDialog("Kinect v1 (SDK 1.8)",
                $"Kinect10.dll no System32: {(runtime ? "OK" : "NÃO ENCONTRADA — instale o Kinect for Windows SDK 1.8")}\n" +
                $"Símbolo {KinectDefine}: {(HasDefine(KinectDefine) ? "ativo" : "inativo")}",
                "OK");
        }

        // ------------------------------------------------------------------ 1. Player Settings

        private static void ApplyPlayerSettings()
        {
            NamedBuildTarget standalone = NamedBuildTarget.Standalone;

            PlayerSettings.SetScriptingBackend(standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetApiCompatibilityLevel(standalone, ApiCompatibilityLevel.NET_Standard);

            // Jogo em TV/projetor, lido de longe.
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;

            // Editar scripts com o jogo rodando: recompila só ao sair do Play
            // (0 = recompilar e continuar, 1 = recompilar ao terminar, 2 = parar e recompilar).
            EditorPrefs.SetInt("ScriptCompilationDuringPlay", 1);

            // Não pausar se alguém clicar em outra janela durante a partida.
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Imitações] Player Settings aplicados (Mono, .NET Standard 2.1, 1920×1080, Windows x86_64).");
        }

        // ------------------------------------------------------------------ 2. Assets de configuração

        private static void CreateConfigAssetsIfMissing()
        {
            EnsureFolder(DataRoot + "/Players");
            EnsureFolder(DataRoot + "/Themes");
            EnsureFolder(DataRoot + "/Settings");

            var players = new[]
            {
                CreatePlayer(1, "Jogador 1", new Color(0.90f, 0.16f, 0.16f), Color.white),  // Vermelho
                CreatePlayer(2, "Jogador 2", new Color(0.16f, 0.72f, 0.26f), Color.white),  // Verde
                CreatePlayer(3, "Jogador 3", new Color(1.00f, 0.84f, 0.10f), Color.black),  // Amarelo
                CreatePlayer(4, "Jogador 4", new Color(0.15f, 0.42f, 0.95f), Color.white),  // Azul
            };

            string rosterPath = DataRoot + "/Settings/PlayerRoster.asset";
            if (AssetDatabase.LoadAssetAtPath<PlayerRoster>(rosterPath) == null)
            {
                var roster = ScriptableObject.CreateInstance<PlayerRoster>();
                var so = new SerializedObject(roster);
                SerializedProperty array = so.FindProperty("players");
                array.arraySize = players.Length;
                for (int i = 0; i < players.Length; i++)
                    array.GetArrayElementAtIndex(i).objectReferenceValue = players[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(roster, rosterPath);
            }

            // O Reset() do ThemeCatalog (que preenche os temas de exemplo) só roda sozinho pelo
            // Inspector/menu Create; via CreateInstance precisamos chamá-lo por reflexão.
            string themesPath = DataRoot + "/Themes/ThemeCatalog.asset";
            if (AssetDatabase.LoadAssetAtPath<ThemeCatalog>(themesPath) == null)
            {
                var catalog = ScriptableObject.CreateInstance<ThemeCatalog>();
                typeof(ThemeCatalog)
                    .GetMethod("Reset", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.Invoke(catalog, null);
                AssetDatabase.CreateAsset(catalog, themesPath);
            }

            string settingsPath = DataRoot + "/Settings/GameSettings.asset";
            if (AssetDatabase.LoadAssetAtPath<GameSettings>(settingsPath) == null)
            {
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GameSettings>(), settingsPath);
            }

            AssetDatabase.SaveAssets();
        }

        private static PlayerConfig CreatePlayer(int number, string displayName, Color color, Color textColor)
        {
            string path = $"{DataRoot}/Players/Jogador{number}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<PlayerConfig>(path);
            if (existing != null) return existing;

            var player = ScriptableObject.CreateInstance<PlayerConfig>();
            var so = new SerializedObject(player);
            so.FindProperty("playerNumber").intValue = number;
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("color").colorValue = color;
            so.FindProperty("contrastTextColor").colorValue = textColor;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(player, path);
            return player;
        }

        // ------------------------------------------------------------------ 3. Kinect v1 (SDK 1.8)

        /// <summary>
        /// Kinect v1 não tem plugin para a Unity: o KinectBodySource chama a Kinect10.dll
        /// (instalada pelo SDK/Runtime 1.8 no System32) via P/Invoke. Aqui só ligamos o símbolo
        /// KINECT_V1 quando essa DLL existe, para o projeto continuar compilando em PCs sem o SDK.
        /// </summary>
        private static void SetupKinect()
        {
            // Pasta do Kinect v2 criada na etapa anterior: removida se não tiver scripts.
            RemoveFolderIfNoScripts("Assets/ThirdParty/KinectV2");
            RemoveFolderIfEmpty("Assets/ThirdParty");

            SetDefine("KINECT_V2", false);
            SetDefine(KinectDefine, IsKinectV1RuntimeInstalled());
        }

        private static bool IsKinectV1RuntimeInstalled()
            => File.Exists(Path.Combine(Environment.SystemDirectory, "Kinect10.dll"));

        private static void RemoveFolderIfNoScripts(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return;
            if (AssetDatabase.FindAssets("t:MonoScript", new[] { folder }).Length > 0) return;
            if (AssetDatabase.DeleteAsset(folder)) Debug.Log($"[Imitações] Removida pasta não usada: {folder}");
        }

        private static void RemoveFolderIfEmpty(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return;
            if (AssetDatabase.GetSubFolders(folder).Length > 0) return;
            if (AssetDatabase.FindAssets("", new[] { folder }).Length > 0) return;
            AssetDatabase.DeleteAsset(folder);
        }

        // ------------------------------------------------------------------ Utilitários

        private static bool HasDefine(string symbol)
        {
            string defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone);
            return defines.Split(';').Any(d => d.Trim() == symbol);
        }

        private static void SetDefine(string symbol, bool enabled)
        {
            if (HasDefine(symbol) == enabled) return;

            var list = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(d => d.Trim())
                .Where(d => d != symbol)
                .ToList();
            if (enabled) list.Add(symbol);

            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Standalone, string.Join(";", list));
            Debug.Log($"[Imitações] Símbolo {symbol} {(enabled ? "ATIVADO" : "desativado")}.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
