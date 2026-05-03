using UnityEngine;
using System.Collections.Generic;

public class ProceduralBotBuilder : MonoBehaviour
{
    // JSON mapping schema matching the WebUI
    [System.Serializable]
    public class BotConfig
    {
        public PartConfig head;
        public PartConfig body;
        public PartConfig arm_l;
        public PartConfig arm_r;
        public PartConfig leg_l;
        public PartConfig leg_r;
    }

    [System.Serializable]
    public class PartConfig
    {
        public string id;
        public string material;
        public string color;
        public string displayName;
    }

    /// <summary>
    /// Builds the full robot based on the JSON configuration.
    /// Returns the root GameObject.
    /// </summary>
    public static GameObject BuildBot(string jsonPayload)
    {
        BotConfig config = JsonUtility.FromJson<BotConfig>(jsonPayload);
        if (config == null)
        {
            Debug.LogError("ProceduralBotBuilder: Failed to parse JSON config.");
            return null;
        }

        GameObject root = new GameObject("AR_AssembledBot");

        // The overall scale down factor since the AR bot might be huge
        float scaleMultiplier = 0.5f;
        root.transform.localScale = Vector3.one * scaleMultiplier;

        // Build each part based on its slot
        if (config.body != null)  BuildPart(root.transform, config.body, "body");
        if (config.head != null)  BuildPart(root.transform, config.head, "head");
        if (config.arm_l != null) BuildPart(root.transform, config.arm_l, "arm_l");
        if (config.arm_r != null) BuildPart(root.transform, config.arm_r, "arm_r");
        if (config.leg_l != null) BuildPart(root.transform, config.leg_l, "leg_l");
        if (config.leg_r != null) BuildPart(root.transform, config.leg_r, "leg_r");

        return root;
    }

    private static void BuildPart(Transform root, PartConfig part, string slot)
    {
        GameObject partObj = new GameObject($"Part_{slot}");
        partObj.transform.SetParent(root, false);

        // Position & Scale based on slot (mirroring WebUI transformations)
        Vector3 localScale = Vector3.one;
        Vector3 localPos = Vector3.zero;
        Vector3 localEuler = Vector3.zero;

        switch (slot)
        {
            case "body":
                localScale = new Vector3(1.5f, 1.5f, 1.5f);
                localPos = new Vector3(0, 0, 0);
                break;
            case "arm_l":
                localScale = new Vector3(0.6f, 1.6f, 0.6f);
                localPos = new Vector3(-1.2f, 0.5f, 0);
                localEuler = new Vector3(0, 0, -35f);
                break;
            case "arm_r":
                localScale = new Vector3(0.6f, 1.6f, 0.6f);
                localPos = new Vector3(1.2f, 0.5f, 0);
                localEuler = new Vector3(0, 0, 35f);
                break;
            case "leg_l":
                localScale = new Vector3(0.7f, 1.4f, 0.7f);
                localPos = new Vector3(-0.6f, -1.8f, 0);
                break;
            case "leg_r":
                localScale = new Vector3(0.7f, 1.4f, 0.7f);
                localPos = new Vector3(0.6f, -1.8f, 0);
                break;
            case "head":
                localScale = new Vector3(1.2f, 1.2f, 1.2f);
                localPos = new Vector3(0, 1.8f, 0);
                break;
        }

        partObj.transform.localScale = localScale;
        partObj.transform.localPosition = localPos;
        partObj.transform.localEulerAngles = localEuler;

        Color mainColor = ParseHtmlColor(part.color, Color.gray);
        
        // Generate the specific 3D model geometry
        GenerateModelGeometry(partObj.transform, part.material, mainColor);
    }

    private static void GenerateModelGeometry(Transform parent, string materialType, Color color)
    {
        Material stdMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        stdMat.color = color;
        stdMat.SetFloat("_Smoothness", 0.3f);

        Material whiteMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        whiteMat.color = Color.white;
        
        Material metalMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        metalMat.color = color;
        metalMat.SetFloat("_Metallic", 0.6f);
        metalMat.SetFloat("_Smoothness", 0.8f);

        // Mapping strings matching React Three Fiber types
        if (materialType.Contains("Bottle"))
        {
            // Hexagonal main body
            CreateMeshChild(parent, "Body", 0.34f, 0.30f, 1.1f, 8, stdMat, new Vector3(0, -0.1f, 0));
            // Shoulder taper
            CreateMeshChild(parent, "Shoulder", 0.16f, 0.34f, 0.32f, 8, stdMat, new Vector3(0, 0.62f, 0));
            // Neck
            CreateMeshChild(parent, "Neck", 0.12f, 0.16f, 0.16f, 6, stdMat, new Vector3(0, 0.86f, 0));
            // Label
            CreateMeshChild(parent, "Label", 0.345f, 0.345f, 0.4f, 8, whiteMat, new Vector3(0, -0.1f, 0));
            // Cap
            CreateMeshChild(parent, "Cap", 0.14f, 0.14f, 0.1f, 6, whiteMat, new Vector3(0, 0.98f, 0));
        }
        else if (materialType.Contains("Can"))
        {
            Material silverMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            silverMat.color = new Color(0.8f, 0.8f, 0.8f);
            silverMat.SetFloat("_Metallic", 0.6f);

            // Octagonal body
            CreateMeshChild(parent, "Body", 0.38f, 0.38f, 1.15f, 8, metalMat, new Vector3(0, 0, 0));
            // Top bevel
            CreateMeshChild(parent, "TopBevel", 0.32f, 0.38f, 0.1f, 8, silverMat, new Vector3(0, 0.62f, 0));
            // Bottom bevel
            CreateMeshChild(parent, "BottomBevel", 0.32f, 0.38f, 0.1f, 8, silverMat, new Vector3(0, -0.62f, 0));
            
            // Tab
            GameObject tab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tab.transform.SetParent(parent, false);
            tab.transform.localScale = new Vector3(0.16f, 0.04f, 0.1f);
            tab.transform.localPosition = new Vector3(0.1f, 0.69f, 0);
            tab.transform.localEulerAngles = new Vector3(0, 0, 5.7f); // ~0.1 rad
            Destroy(tab.GetComponent<Collider>());
            tab.GetComponent<MeshRenderer>().sharedMaterial = silverMat;
        }
        else if (materialType.Contains("Cup"))
        {
            Material cupWhite = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            cupWhite.color = new Color(0.96f, 0.94f, 0.9f);

            // Hexagonal tapered
            CreateMeshChild(parent, "Body", 0.38f, 0.26f, 1.1f, 8, cupWhite, new Vector3(0, 0, 0));
            // Sleeve
            CreateMeshChild(parent, "Sleeve", 0.385f, 0.285f, 0.5f, 8, stdMat, new Vector3(0, -0.08f, 0));

            if (materialType.Contains("Coffee"))
            {
                Material lidMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                lidMat.color = new Color(0.1f, 0.1f, 0.1f);
                CreateMeshChild(parent, "Lid", 0.42f, 0.38f, 0.1f, 8, lidMat, new Vector3(0, 0.58f, 0));
            }
        }
        else // Fallback for straws or paper
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.transform.SetParent(parent, false);
            box.transform.localScale = new Vector3(0.5f, 1.0f, 0.5f);
            Destroy(box.GetComponent<Collider>());
            box.GetComponent<MeshRenderer>().sharedMaterial = stdMat;
        }
    }

    /// <summary>
    /// Helper to create a polygonal cylinder mesh dynamically to match React Three Fiber's cylinderGeometry.
    /// </summary>
    private static void CreateMeshChild(Transform parent, string name, float radiusTop, float radiusBottom, float height, int radialSegments, Material mat, Vector3 localPos)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;

        MeshFilter mf = go.AddComponent<MeshFilter>();
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;

        Mesh mesh = GeneratePolygonalCylinder(radiusTop, radiusBottom, height, radialSegments);
        mf.sharedMesh = mesh;
    }

    private static Mesh GeneratePolygonalCylinder(float rt, float rb, float h, int segments)
    {
        Mesh mesh = new Mesh();
        
        int numVertices = (segments + 1) * 2 + segments * 2; // top, bottom, and side faces (duplicated for flat shading)
        // Actually, for true flat shading like ThreeJS flatShading, it's easier to compute all triangles individually.
        
        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();

        float yTop = h / 2f;
        float yBot = -h / 2f;

        // Build sides
        for (int i = 0; i < segments; i++)
        {
            float angle1 = ((float)i / segments) * Mathf.PI * 2f;
            float angle2 = ((float)(i + 1) / segments) * Mathf.PI * 2f;

            Vector3 t1 = new Vector3(Mathf.Cos(angle1) * rt, yTop, Mathf.Sin(angle1) * rt);
            Vector3 t2 = new Vector3(Mathf.Cos(angle2) * rt, yTop, Mathf.Sin(angle2) * rt);
            Vector3 b1 = new Vector3(Mathf.Cos(angle1) * rb, yBot, Mathf.Sin(angle1) * rb);
            Vector3 b2 = new Vector3(Mathf.Cos(angle2) * rb, yBot, Mathf.Sin(angle2) * rb);

            int start = verts.Count;
            // Quad -> 2 triangles
            verts.Add(t1); verts.Add(t2); verts.Add(b2); verts.Add(b1);
            tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
            tris.Add(start); tris.Add(start + 2); tris.Add(start + 3);
        }

        // Top Cap
        Vector3 centerTop = new Vector3(0, yTop, 0);
        for (int i = 0; i < segments; i++)
        {
            float angle1 = ((float)i / segments) * Mathf.PI * 2f;
            float angle2 = ((float)(i + 1) / segments) * Mathf.PI * 2f;
            Vector3 t1 = new Vector3(Mathf.Cos(angle1) * rt, yTop, Mathf.Sin(angle1) * rt);
            Vector3 t2 = new Vector3(Mathf.Cos(angle2) * rt, yTop, Mathf.Sin(angle2) * rt);

            int start = verts.Count;
            verts.Add(centerTop); verts.Add(t2); verts.Add(t1);
            tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
        }

        // Bottom Cap
        Vector3 centerBot = new Vector3(0, yBot, 0);
        for (int i = 0; i < segments; i++)
        {
            float angle1 = ((float)i / segments) * Mathf.PI * 2f;
            float angle2 = ((float)(i + 1) / segments) * Mathf.PI * 2f;
            Vector3 b1 = new Vector3(Mathf.Cos(angle1) * rb, yBot, Mathf.Sin(angle1) * rb);
            Vector3 b2 = new Vector3(Mathf.Cos(angle2) * rb, yBot, Mathf.Sin(angle2) * rb);

            int start = verts.Count;
            verts.Add(centerBot); verts.Add(b1); verts.Add(b2);
            tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
        }

        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals(); // Crucial for flat shading look
        mesh.RecalculateBounds();

        return mesh;
    }

    private static Color ParseHtmlColor(string hex, Color defaultColor)
    {
        if (string.IsNullOrEmpty(hex)) return defaultColor;
        if (!hex.StartsWith("#")) hex = "#" + hex;
        if (ColorUtility.TryParseHtmlString(hex, out Color col))
            return col;
        return defaultColor;
    }
}