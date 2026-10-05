using System;
using System.IO;
using System.Linq;
using Imitacoes.Body;
using Imitacoes.Config;
using Imitacoes.Game;
using Imitacoes.Interaction;
using Imitacoes.Motion;
using Imitacoes.Scoring;
using Imitacoes.Sources;
using Imitacoes.UI;
using Imitacoes.Visual;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Imitacoes.EditorTools
{
    /// <summary>
    /// Menu Imitações ▸ Criar Cena Principal: monta Main.unity inteira, com todos os objetos,
    /// componentes e referências do Inspector já ligados. Pode ser rodado de novo a qualquer
    /// momento (recria a cena do zero).
    ///
    /// Duas versões: bonecos 3D (padrão) ou bonecos 2D desenhados na UI.
    ///
    /// Hierarquia gerada (3D):
    /// <code>
    /// Main Camera           perspectiva, de frente para os bonecos
    /// Luz Principal / Luz de Recorte
    /// [Fundo]               Canvas Screen Space - Camera (atrás dos bonecos): brilhos e bolhas
    /// [Palco 3D]
    ///   Boneco Ao Vivo      Avatar3DView
    ///   Boneco Clipe        Avatar3DView (replay e fantasma)
    /// [Sistemas]
    ///   Kinect v1           KinectBodySource (se KINECT_V1 estiver ativo)
    ///   Mock                MockBodySource
    ///   Jogador Ativo       ActiveBodySelector  → Kinect v1, Mock
    ///   Gestos              GestureDetector     → Jogador Ativo
    ///   Gravador            MotionRecorder      → Jogador Ativo
    ///   Comparador          MotionComparer
    /// [UI]                  Canvas + CanvasScaler + GameUI + HandCursor + MotionClipPlayer
    ///   (telas geradas pelo GameUI.BuildLayout)
    /// [GameManager]         GameManager + AudioSource → tudo acima + assets de Data/
    /// </code>
    /// </summary>
    internal static class SceneBuilder
    {
        private const string ScenePath = "Assets/_Imitacoes/Scenes/Main.unity";
        private const string DataRoot = "Assets/_Imitacoes/Data";
        private const string TmpEssentials = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";
        private const string MaterialsFolder = "Assets/_Imitacoes/Materials";

        private static bool build3D = true;

        [MenuItem("Imitações/Criar Cena Principal", priority = 1)]
        private static void CreateMainSceneMenu()
        {
            build3D = true;
            StartBuild();
        }

        [MenuItem("Imitações/Criar Cena Principal (bonecos 2D)", priority = 2)]
        private static void CreateMainScene2DMenu()
        {
            build3D = false;
            StartBuild();
        }

        private static void StartBuild()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // Sem o TMP Essentials os textos ficam invisíveis: importa e continua depois.
            if (!HasTmpEssentials())
            {
                string package = Path.GetFullPath(TmpEssentials);
                if (File.Exists(package))
                {
                    Debug.Log("[Imitações] Importando TMP Essential Resources...");
                    AssetDatabase.importPackageCompleted += OnTmpImported;
                    AssetDatabase.ImportPackage(package, false);
                    return;
                }
                Debug.LogWarning("[Imitações] TMP Essentials não encontrado. Use Window ▸ TextMeshPro ▸ Import TMP Essential Resources.");
            }

            BuildScene();
        }

        private static void OnTmpImported(string packageName)
        {
            AssetDatabase.importPackageCompleted -= OnTmpImported;
            EditorApplication.delayCall += BuildScene;
        }

        private static bool HasTmpEssentials()
            => AssetDatabase.FindAssets("t:TMP_Settings").Length > 0;

        private static void BuildScene()
        {
            try
            {
                BuildSceneInternal();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Imitações", "Erro ao montar a cena:\n" + e.Message, "OK");
            }
        }

        private static void BuildSceneInternal()
        {
            // Assets de configuração (cria se faltar).
            EditorApplication.ExecuteMenuItem("Imitações/Configurar Projeto (reaplicar tudo)");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Carregar DEPOIS do NewScene: criar a cena descarrega assets não usados,
            // e referências obtidas antes viram nulas.
            var settings = Load<GameSettings>("Settings/GameSettings.asset");
            var roster = Load<PlayerRoster>("Settings/PlayerRoster.asset");
            var themes = Load<ThemeCatalog>("Themes/ThemeCatalog.asset");

            // ---------------------------------------------------------- Câmera
            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Palette.Background;
            cameraGo.AddComponent<AudioListener>();

            if (build3D)
            {
                // Olha os bonecos de frente (eles ficam em z = 0, com o rosto para +Z).
                camera.orthographic = false;
                camera.fieldOfView = 28f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 100f;
                cameraGo.transform.position = new Vector3(0f, 1.75f, 7.3f);
                cameraGo.transform.LookAt(new Vector3(0f, 1.45f, 0f));
            }
            else
            {
                camera.orthographic = true;
            }

            // ---------------------------------------------------------- Sistemas
            var systems = new GameObject("[Sistemas]").transform;

            BodySourceBehaviour kinect = null;
            Type kinectType = TypeCache.GetTypesDerivedFrom<BodySourceBehaviour>()
                .FirstOrDefault(t => t.FullName == "Imitacoes.Kinect.KinectBodySource");
            if (kinectType != null)
            {
                kinect = (BodySourceBehaviour)Child(systems, "Kinect v1").AddComponent(kinectType);
            }
            else
            {
                Debug.LogWarning("[Imitações] KinectBodySource não compilado (símbolo KINECT_V1 inativo): a cena usará só o mock.");
            }

            var mock = Child(systems, "Mock").AddComponent<MockBodySource>();
            var selector = Child(systems, "Jogador Ativo").AddComponent<ActiveBodySelector>();
            Set(selector, "kinectSource", kinect);
            Set(selector, "mockSource", mock);

            var gestures = Child(systems, "Gestos").AddComponent<GestureDetector>();
            Set(gestures, "bodyProvider", selector);

            var recorder = Child(systems, "Gravador").AddComponent<MotionRecorder>();
            Set(recorder, "bodyProvider", selector);

            var comparer = Child(systems, "Comparador").AddComponent<MotionComparer>();

            // ---------------------------------------------------------- UI
            var uiGo = new GameObject("[UI]", typeof(RectTransform));
            var canvas = uiGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = uiGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var gameUI = uiGo.AddComponent<GameUI>();
            UISkin skin = SkinAssets.Create(); // sprites arredondados + fonte Lilita One

            HandCursor cursor;
            if (build3D)
            {
                Transform background = CreateBackgroundCanvas(camera);
                (Avatar3DView live, Avatar3DView clip) = CreateStage3D();
                cursor = gameUI.BuildLayout(skin, background, live, clip);
            }
            else
            {
                cursor = gameUI.BuildLayout(skin);
            }
            Set(cursor, "bodyProvider", selector);

            var clipPlayer = uiGo.AddComponent<MotionClipPlayer>();
            clipPlayer.SetView(gameUI.ClipView);

            // ---------------------------------------------------------- GameManager
            var managerGo = new GameObject("[GameManager]");
            var audio = managerGo.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            var manager = managerGo.AddComponent<GameManager>();
            Set(manager, "settings", settings);
            Set(manager, "roster", roster);
            Set(manager, "themes", themes);
            Set(manager, "body", selector);
            Set(manager, "gestures", gestures);
            Set(manager, "recorder", recorder);
            Set(manager, "comparer", comparer);
            Set(manager, "ui", gameUI);
            Set(manager, "handCursor", cursor);
            Set(manager, "clipPlayer", clipPlayer);
            Set(manager, "audioSource", audio);

            // ---------------------------------------------------------- Salvar
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath) ?? "Assets");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Selection.activeGameObject = managerGo;

            Debug.Log($"[Imitações] Cena {(build3D ? "3D" : "2D")} criada em {ScenePath}. Dê Play para testar (sem Kinect: mouse = mão, Espaço = mãos para cima).");
        }

        // ------------------------------------------------------------------ 3D

        /// <summary>Canvas do fundo, desenhado pela câmera ATRÁS dos bonecos 3D.</summary>
        private static Transform CreateBackgroundCanvas(Camera camera)
        {
            var go = new GameObject("[Fundo]", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 40f;
            canvas.sortingOrder = -100;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return go.transform;
        }

        /// <summary>Luzes, materiais e os dois bonecos 3D.</summary>
        private static (Avatar3DView live, Avatar3DView clip) CreateStage3D()
        {
            // Iluminação: ambiente azulado + luz principal de frente + luz de recorte por trás.
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.44f, 0.58f);

            var key = new GameObject("Luz Principal").AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1f, 0.96f, 0.9f);
            key.intensity = 1.25f;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.65f;
            key.transform.rotation = Quaternion.Euler(35f, 200f, 0f);

            var rimLight = new GameObject("Luz de Recorte").AddComponent<Light>();
            rimLight.type = LightType.Directional;
            rimLight.color = new Color(0.55f, 0.65f, 1f);
            rimLight.intensity = 1.3f;
            rimLight.shadows = LightShadows.None;
            rimLight.transform.rotation = Quaternion.Euler(15f, 20f, 0f);

            Material body = EnsureMaterial("Boneco.mat", false, Color.white, 0.55f);
            Material eyes = EnsureMaterial("Olhos.mat", false, new Color(0.03f, 0.03f, 0.06f), 0.9f);
            Material platform = EnsureMaterial("Plataforma.mat", false, new Color(0.12f, 0.13f, 0.22f), 0.3f);
            Material glow = EnsureMaterial("Brilho.mat", true, Color.white, 0f);

            var stage = new GameObject("[Palco 3D]").transform;
            Avatar3DView clip = CreateAvatar(stage, "Boneco Clipe (Replay/Fantasma)", body, eyes, platform, glow);
            Avatar3DView live = CreateAvatar(stage, "Boneco Ao Vivo", body, eyes, platform, glow);
            return (live, clip);
        }

        private static Avatar3DView CreateAvatar(Transform parent, string name, Material body, Material eyes, Material platform, Material glow)
        {
            var view = Child(parent, name).AddComponent<Avatar3DView>();
            Set(view, "bodyMaterial", body);
            Set(view, "eyeMaterial", eyes);
            Set(view, "platformMaterial", platform);
            Set(view, "glowMaterial", glow);
            return view;
        }

        private static Material EnsureMaterial(string file, bool unlit, Color color, float smoothness)
        {
            Directory.CreateDirectory(MaterialsFolder);
            string path = $"{MaterialsFolder}/{file}";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit")
                            ?? Shader.Find(unlit ? "Unlit/Color" : "Standard");
            material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // ------------------------------------------------------------------ Utilitários

        private static GameObject Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static T Load<T>(string relativePath) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>($"{DataRoot}/{relativePath}");
            if (asset == null) Debug.LogError($"[Imitações] Asset não encontrado: {DataRoot}/{relativePath}");
            return asset;
        }

        /// <summary>Atribui um campo [SerializeField] privado, como se fosse pelo Inspector.</summary>
        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[Imitações] Campo '{field}' não existe em {target.GetType().Name}.");
                return;
            }
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
