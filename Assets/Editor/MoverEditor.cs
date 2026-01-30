#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MoverKinematic))]
[CanEditMultipleObjects]
public class MoverEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("Use the scene handles to position Start/End.\nBlue = Start, Orange = End.", MessageType.Info);
    }

    void OnSceneGUI()
    {
        var mover = target as MoverKinematic;
        if (mover == null) return;

        var t = mover.transform;

        // Resolve world positions based on mover.useLocal
        Vector3 startW = mover.useLocal ? ToWorld(mover.startPos, t) : mover.startPos;
        Vector3 endW = mover.useLocal ? ToWorld(mover.endPos, t) : mover.endPos;

        // Draw path line
        Handles.color = new Color(0f, 0.8f, 1f, 0.7f);
        Handles.DrawAAPolyLine(4f, startW, endW);

        // Draw endpoints
        float s0 = HandleUtility.GetHandleSize(startW) * 0.1f;
        float s1 = HandleUtility.GetHandleSize(endW) * 0.1f;

        Handles.color = new Color(0f, 0.8f, 1f, 0.9f); // Start = blue-ish
        Handles.SphereHandleCap(0, startW, Quaternion.identity, s0, EventType.Repaint);

        Handles.color = new Color(1f, 0.6f, 0f, 0.9f); // End = orange
        Handles.SphereHandleCap(0, endW, Quaternion.identity, s1, EventType.Repaint);

        // Editable position handles
        EditorGUI.BeginChangeCheck();
        Vector3 newStartW = Handles.PositionHandle(startW, Quaternion.identity);
        Vector3 newEndW = Handles.PositionHandle(endW, Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(mover, "Move Start/End");
            if (mover.useLocal)
            {
                mover.startPos = ToLocal(newStartW, t);
                mover.endPos = ToLocal(newEndW, t);
            }
            else
            {
                mover.startPos = newStartW;
                mover.endPos = newEndW;
            }
            EditorUtility.SetDirty(mover);
        }

        // Labels
        Handles.BeginGUI();
        var sv = SceneView.currentDrawingSceneView;
        if (sv != null)
        {
            var cam = sv.camera;
            Vector3 sp1 = cam.WorldToScreenPoint(startW);
            Vector3 sp2 = cam.WorldToScreenPoint(endW);
            GUI.color = Color.white;
            GUI.Label(new Rect(sp1.x + 8, cam.pixelHeight - sp1.y - 16, 140, 20), "Start");
            GUI.Label(new Rect(sp2.x + 8, cam.pixelHeight - sp2.y - 16, 140, 20), "End");
        }
        Handles.EndGUI();
    }

    static Vector3 ToWorld(Vector3 localPos, Transform t)
    {
        var parent = t.parent;
        return parent ? parent.TransformPoint(localPos) : localPos;
    }
    static Vector3 ToLocal(Vector3 worldPos, Transform t)
    {
        var parent = t.parent;
        return parent ? parent.InverseTransformPoint(worldPos) : worldPos;
    }
}
#endif
