using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class NPCOutline : MonoBehaviour
{
    [Header("References")]
    [SerializeField] NPCProfile _profile;
    [SerializeField] Material _outlineMaterial;

    [Header("Outline")]
    [SerializeField] float _outlineWidth = 0.02f;

    readonly List<GameObject> _outlineObjects = new();
    readonly List<Mesh> _outlineMeshes = new();

    void Awake()
    {
        if (_profile == null)
        {
            TryGetComponent(out _profile);
        }

        CreateOutlineRenderers();
    }

    void OnEnable()
    {
        if (_profile == null)
            return;

        _profile.MarkedStateChanged += OnMarkedStateChanged;

        ApplyOutlineState(_profile.IsMarked);
    }

    void OnDisable()
    {
        if (_profile == null)
            return;

        _profile.MarkedStateChanged -= OnMarkedStateChanged;
    }

    void OnDestroy()
    {
        DestroyOutlineRenderers();
    }

    void OnMarkedStateChanged(
    NPCProfile profile,
    bool isMarked)
    {
        ApplyOutlineState(isMarked);
    }

    Mesh CreateOutlineMesh(SkinnedMeshRenderer source)
    {
        Mesh sourceMesh = source.sharedMesh;

        if (sourceMesh == null)
            return null;

        Mesh outlineMesh =
            Instantiate(sourceMesh);

        outlineMesh.name =
            $"{sourceMesh.name}_Outline";

        Vector3[] vertices =
            outlineMesh.vertices;

        int[] triangles =
            outlineMesh.triangles;

        Vector3[] smoothNormals =
            GenerateSmoothNormals(
                vertices,
                triangles);

        outlineMesh.normals =
            smoothNormals;

        _outlineMeshes.Add(outlineMesh);

        return outlineMesh;
    }

    Vector3[] GenerateSmoothNormals(
    Vector3[] vertices,
    int[] triangles)
    {
        Vector3[] normals =
            new Vector3[vertices.Length];

        Dictionary<Vector3Int, List<int>> vertexGroups =
            new();

        const float tolerance = 0.0001f;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3Int key = new Vector3Int(
                Mathf.RoundToInt(vertices[i].x / tolerance),
                Mathf.RoundToInt(vertices[i].y / tolerance),
                Mathf.RoundToInt(vertices[i].z / tolerance));

            if (!vertexGroups.TryGetValue(
                    key,
                    out List<int> indices))
            {
                indices = new List<int>();
                vertexGroups.Add(key, indices);
            }

            indices.Add(i);
        }

        for (int i = 0; i < triangles.Length; i += 3)
        {
            int indexA = triangles[i];
            int indexB = triangles[i + 1];
            int indexC = triangles[i + 2];

            Vector3 vertexA =
                vertices[indexA];

            Vector3 vertexB =
                vertices[indexB];

            Vector3 vertexC =
                vertices[indexC];

            Vector3 edgeA =
                vertexB - vertexA;

            Vector3 edgeB =
                vertexC - vertexA;

            Vector3 faceNormal =
                Vector3.Cross(edgeA, edgeB);

            normals[indexA] += faceNormal;
            normals[indexB] += faceNormal;
            normals[indexC] += faceNormal;
        }

        foreach (List<int> indices in vertexGroups.Values)
        {
            Vector3 averagedNormal = Vector3.zero;

            for (int i = 0; i < indices.Count; i++)
            {
                averagedNormal +=
                    normals[indices[i]];
            }

            averagedNormal.Normalize();

            for (int i = 0; i < indices.Count; i++)
            {
                normals[indices[i]] =
                    averagedNormal;
            }
        }

        for (int i = 0; i < normals.Length; i++)
        {
            normals[i].Normalize();
        }

        return normals;
    }

    void CreateOutlineRenderers()
    {
        if (_outlineMaterial == null)
        {
            Debug.LogError(
                $"NPCOutline on {name} has no outline material assigned.",
                this);

            return;
        }

        SkinnedMeshRenderer[] sourceRenderers =
            GetComponentsInChildren<SkinnedMeshRenderer>(false);

        foreach (SkinnedMeshRenderer sourceRenderer in sourceRenderers)
        {
            CreateOutlineRenderer(sourceRenderer);
        }
    }

    void CreateOutlineRenderer(
        SkinnedMeshRenderer sourceRenderer)
    {
        GameObject outlineObject =
            new($"{sourceRenderer.name}_Outline");

        Transform sourceTransform =
            sourceRenderer.transform;

        Transform outlineTransform =
            outlineObject.transform;

        outlineTransform.SetParent(
            sourceTransform.parent,
            false);


        outlineTransform.SetLocalPositionAndRotation(
sourceTransform.localPosition,
sourceTransform.localRotation);
        outlineTransform.localScale =
            sourceTransform.localScale;

        SkinnedMeshRenderer outlineRenderer =
            outlineObject.AddComponent<SkinnedMeshRenderer>();

        CopySkinnedMeshRenderer(
            sourceRenderer,
            outlineRenderer);

        _outlineObjects.Add(outlineObject);
    }

    void CopySkinnedMeshRenderer(
        SkinnedMeshRenderer source,
        SkinnedMeshRenderer destination)
    {
        destination.sharedMesh = CreateOutlineMesh(source);

        destination.bones = source.bones;

        destination.rootBone = source.rootBone;

        destination.localBounds = source.localBounds;

        destination.quality = source.quality;

        destination.updateWhenOffscreen = true;

        destination.skinnedMotionVectors = source.skinnedMotionVectors;

        destination.allowOcclusionWhenDynamic = source.allowOcclusionWhenDynamic;

        destination.forceMatrixRecalculationPerRender = source.forceMatrixRecalculationPerRender;

        destination.shadowCastingMode = ShadowCastingMode.Off;

        destination.receiveShadows = false;

        destination.lightProbeUsage = LightProbeUsage.Off;

        destination.reflectionProbeUsage = ReflectionProbeUsage.Off;

        int materialCount =
            source.sharedMaterials.Length;

        Material[] materials =
            new Material[materialCount];

        for (int i = 0; i < materialCount; i++)
        {
            materials[i] = _outlineMaterial;
        }

        destination.sharedMaterials = materials;

        if (source.sharedMesh != null)
        {
            int blendShapeCount =
                source.sharedMesh.blendShapeCount;

            for (int i = 0; i < blendShapeCount; i++)
            {
                destination.SetBlendShapeWeight(
                    i,
                    source.GetBlendShapeWeight(i));
            }
        }
    }

    void ApplyOutlineState(bool isMarked)
    {
        for (int i = 0; i < _outlineObjects.Count; i++)
        {
            if (_outlineObjects[i] == null)
                continue;

            _outlineObjects[i].SetActive(isMarked);
        }
    }

    void DestroyOutlineRenderers()
    {
        for (int i = 0; i < _outlineObjects.Count; i++)
        {
            if (_outlineObjects[i] == null)
                continue;

            Destroy(_outlineObjects[i]);
        }

        for (int i = 0; i < _outlineMeshes.Count; i++)
        {
            if (_outlineMeshes[i] == null)
                continue;

            Destroy(_outlineMeshes[i]);
        }

        _outlineObjects.Clear();
        _outlineMeshes.Clear();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (_profile == null) TryGetComponent(out _profile);
    }
#endif
}