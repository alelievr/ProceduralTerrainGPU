using UnityEngine;
using UnityEditor;

// [CustomEditor(typeof(MeshFilter))]
public class MeshNormalDebugger : Editor
{
    Mesh        mesh;
    Transform   transform;

    private void OnEnable()
    {
        mesh = (target as MeshFilter).sharedMesh;
        transform = (target as MeshFilter).transform;
    }

    void OnSceneGUI()
    {
        Vector3 objectPos = transform.position;

        if (Event.current.type != EventType.Repaint)
            return ;

        for (int i = 0; i < mesh.vertexCount; i++)
        {
            Vector3 vertex = mesh.vertices[i] + objectPos;
            Vector3 normal = mesh.normals[i];

            Vector3 n = (normal / 2.0f) + Vector3.one * 0.5f;
            Handles.color = new Color(n.x, n.y, n.z, 1);
            Handles.ArrowHandleCap(0, vertex, Quaternion.LookRotation(-normal), .5f, EventType.Repaint);
        }
    }
}