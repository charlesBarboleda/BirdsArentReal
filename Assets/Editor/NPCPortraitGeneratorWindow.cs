#if UNITY_EDITOR

using System.IO;
using UnityEditor;
using UnityEngine;

public class NPCPortraitGeneratorWindow : EditorWindow
{
    static readonly Vector3 PortraitPreviewPosition =
        new Vector3(0f, 10000f, 0f);

    [Header("NPC")]
    [SerializeField] GameObject _npcPrefab;

    [Header("Output")]
    [SerializeField] string _outputFolder = "Assets/NPCs/Portraits";
    [SerializeField] int _resolution = 512;

    [Header("Camera")]
    [SerializeField] float _fieldOfView = 30f;
    [SerializeField] float _nearClipPlane = 0.01f;
    [SerializeField] float _farClipPlane = 100f;

    GameObject _previewInstance;
    GameObject _cameraObject;
    RenderTexture _renderTexture;

    const string PortraitFieldName = "_portrait";

    [MenuItem("Tools/NPC Portrait Generator")]
    public static void OpenWindow()
    {
        GetWindow<NPCPortraitGeneratorWindow>(
            "NPC Portrait Generator");
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField(
            "NPC Portrait Generator",
            EditorStyles.boldLabel);

        EditorGUILayout.Space();

        _npcPrefab = (GameObject)EditorGUILayout.ObjectField(
            "NPC Prefab",
            _npcPrefab,
            typeof(GameObject),
            false);

        EditorGUILayout.Space();

        EditorGUILayout.LabelField(
            "Output Settings",
            EditorStyles.boldLabel);

        _outputFolder = EditorGUILayout.TextField(
            "Output Folder",
            _outputFolder);

        _resolution = EditorGUILayout.IntField(
            "Resolution",
            _resolution);

        _resolution = Mathf.Max(32, _resolution);

        EditorGUILayout.Space();

        EditorGUILayout.LabelField(
            "Camera Settings",
            EditorStyles.boldLabel);

        _fieldOfView = EditorGUILayout.FloatField(
            "Field of View",
            _fieldOfView);

        _nearClipPlane = EditorGUILayout.FloatField(
            "Near Clip Plane",
            _nearClipPlane);

        _farClipPlane = EditorGUILayout.FloatField(
            "Far Clip Plane",
            _farClipPlane);

        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "The portrait background will be fully transparent. " +
            "The NPC model itself will remain visible.",
            MessageType.Info);

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(_npcPrefab == null))
        {
            if (GUILayout.Button("Generate Portrait"))
            {
                GeneratePortrait();
            }
        }
    }

    void GeneratePortrait()
    {
        if (_npcPrefab == null)
        {
            Debug.LogError("No NPC prefab selected.");
            return;
        }

        NPCPortraitSource prefabSource =
            _npcPrefab.GetComponent<NPCPortraitSource>();

        if (prefabSource == null)
        {
            Debug.LogError(
                $"'{_npcPrefab.name}' does not have an NPCPortraitSource component.");

            return;
        }

        if (prefabSource.PortraitCameraAnchor == null)
        {
            Debug.LogError(
                $"'{_npcPrefab.name}' has no PortraitCameraAnchor assigned.");

            return;
        }

        if (prefabSource.PortraitLookTarget == null)
        {
            Debug.LogError(
                $"'{_npcPrefab.name}' has no PortraitLookTarget assigned.");

            return;
        }

        EnsureOutputFolderExists();

        try
        {
            CreatePreviewInstance();
            CreatePortraitCamera();

            NPCPortraitSource previewSource =
                _previewInstance.GetComponent<NPCPortraitSource>();

            Transform cameraAnchor =
                previewSource.PortraitCameraAnchor;

            Transform lookTarget =
                previewSource.PortraitLookTarget;

            Camera camera =
                _cameraObject.GetComponent<Camera>();

            // Position the camera using the NPC's configured anchor.
            camera.transform.position =
                cameraAnchor.position;

            // Aim the camera at the configured look target.
            Vector3 lookDirection =
                lookTarget.position -
                camera.transform.position;

            if (lookDirection.sqrMagnitude <= 0.0001f)
            {
                Debug.LogError(
                    $"'{_npcPrefab.name}' has a PortraitCameraAnchor and PortraitLookTarget that are too close together.");

                return;
            }

            camera.transform.rotation =
                Quaternion.LookRotation(
                    lookDirection.normalized,
                    Vector3.up);

            CapturePortrait();

            Debug.Log(
                $"Successfully generated and assigned portrait for '{_npcPrefab.name}'.");
        }
        finally
        {
            CleanupPreview();
        }
    }

    void CreatePreviewInstance()
    {
        _previewInstance =
            (GameObject)PrefabUtility.InstantiatePrefab(_npcPrefab);

        _previewInstance.name =
            $"{_npcPrefab.name}_PortraitPreview";

        // Move the temporary NPC far away from the actual game scene.
        _previewInstance.transform.position =
            PortraitPreviewPosition;

        DisableGameplayComponents(_previewInstance);
    }

    void CreatePortraitCamera()
    {
        _cameraObject = new GameObject(
            $"{_npcPrefab.name}_PortraitCamera");

        Camera camera =
            _cameraObject.AddComponent<Camera>();

        camera.fieldOfView = _fieldOfView;
        camera.nearClipPlane = _nearClipPlane;
        camera.farClipPlane = _farClipPlane;

        camera.clearFlags =
            CameraClearFlags.SolidColor;

        // Alpha = 0 means transparent background.
        camera.backgroundColor =
            new Color(0f, 0f, 0f, 0f);

        camera.allowHDR = false;
        camera.allowMSAA = false;

        _renderTexture = new RenderTexture(
            _resolution,
            _resolution,
            24,
            RenderTextureFormat.ARGB32);

        _renderTexture.name =
            $"{_npcPrefab.name}_PortraitRenderTexture";

        _renderTexture.Create();

        camera.targetTexture = _renderTexture;
    }

    void CapturePortrait()
    {
        Camera camera =
            _cameraObject.GetComponent<Camera>();

        RenderTexture previousActive =
            RenderTexture.active;

        RenderTexture.active =
            _renderTexture;

        camera.Render();

        Texture2D texture = new Texture2D(
            _resolution,
            _resolution,
            TextureFormat.RGBA32,
            false);

        texture.ReadPixels(
            new Rect(
                0,
                0,
                _resolution,
                _resolution),
            0,
            0);

        texture.Apply();

        byte[] pngData =
            texture.EncodeToPNG();

        string fileName =
            $"{_npcPrefab.name}_Portrait.png";

        string assetPath =
            $"{_outputFolder}/{fileName}";

        string absolutePath =
            Path.GetFullPath(assetPath);

        File.WriteAllBytes(
            absolutePath,
            pngData);

        DestroyImmediate(texture);

        RenderTexture.active =
            previousActive;

        AssetDatabase.ImportAsset(
            assetPath,
            ImportAssetOptions.ForceUpdate);

        ConfigureTextureImporter(assetPath);

        AssetDatabase.Refresh();

        Sprite portrait =
            AssetDatabase.LoadAssetAtPath<Sprite>(
                assetPath);

        if (portrait == null)
        {
            Debug.LogError(
                $"Portrait was generated, but Unity could not load it as a Sprite: {assetPath}");

            return;
        }

        AssignPortraitToPrefab(portrait);
    }

    void ConfigureTextureImporter(string assetPath)
    {
        TextureImporter importer =
            AssetImporter.GetAtPath(assetPath)
            as TextureImporter;

        if (importer == null)
        {
            Debug.LogError(
                $"Could not find TextureImporter for '{assetPath}'.");

            return;
        }

        importer.textureType =
            TextureImporterType.Sprite;

        importer.spriteImportMode =
            SpriteImportMode.Single;

        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;

        importer.SaveAndReimport();
    }

    void AssignPortraitToPrefab(Sprite portrait)
    {
        string prefabPath =
            AssetDatabase.GetAssetPath(_npcPrefab);

        if (string.IsNullOrEmpty(prefabPath))
        {
            Debug.LogError(
                $"Could not determine prefab asset path for '{_npcPrefab.name}'.");

            return;
        }

        GameObject prefabRoot =
            PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            NPCProfile profile =
                prefabRoot.GetComponent<NPCProfile>();

            if (profile == null)
            {
                Debug.LogError(
                    $"'{_npcPrefab.name}' does not contain an NPCProfile component.");

                return;
            }

            SerializedObject serializedProfile =
                new SerializedObject(profile);

            SerializedProperty portraitProperty =
                serializedProfile.FindProperty(
                    PortraitFieldName);

            if (portraitProperty == null)
            {
                Debug.LogError(
                    $"Could not find a serialized field named '{PortraitFieldName}' on NPCProfile.");

                return;
            }

            portraitProperty.objectReferenceValue =
                portrait;

            serializedProfile.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(
                prefabRoot,
                prefabPath);

            Debug.Log(
                $"Assigned '{portrait.name}' to {prefabPath}.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    void DisableGameplayComponents(GameObject instance)
    {
        MonoBehaviour[] behaviours =
            instance.GetComponentsInChildren<MonoBehaviour>(
                true);

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null)
                continue;

            // Keep the portrait source available.
            if (behaviour is NPCPortraitSource)
                continue;

            behaviour.enabled = false;
        }

        Collider[] colliders =
            instance.GetComponentsInChildren<Collider>(
                true);

        foreach (Collider collider in colliders)
        {
            collider.enabled = false;
        }

        Rigidbody[] rigidbodies =
            instance.GetComponentsInChildren<Rigidbody>(
                true);

        foreach (Rigidbody rigidbody in rigidbodies)
        {
            rigidbody.isKinematic = true;
            rigidbody.detectCollisions = false;
        }
    }

    void EnsureOutputFolderExists()
    {
        if (!AssetDatabase.IsValidFolder("Assets/NPCs"))
        {
            AssetDatabase.CreateFolder(
                "Assets",
                "NPCs");
        }

        if (!AssetDatabase.IsValidFolder(_outputFolder))
        {
            AssetDatabase.CreateFolder(
                "Assets/NPCs",
                "Portraits");
        }
    }

    void CleanupPreview()
    {
        if (_previewInstance != null)
        {
            DestroyImmediate(_previewInstance);
            _previewInstance = null;
        }

        if (_cameraObject != null)
        {
            DestroyImmediate(_cameraObject);
            _cameraObject = null;
        }

        if (_renderTexture != null)
        {
            _renderTexture.Release();
            DestroyImmediate(_renderTexture);
            _renderTexture = null;
        }
    }

    void OnDisable()
    {
        CleanupPreview();
    }
}

#endif