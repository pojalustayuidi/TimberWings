using UnityEditor;
using UnityEngine;

// Static rig checks, not production animation clips and never gameplay callbacks.
public sealed class LeoPosePreview : EditorWindow
{
    public enum Pose{Rest,TPose,AxeReady,AxeContact,CarryFull,BuildReady,Talk,Compass}
    LeoRig rig;Pose pose;
    [MenuItem(LeoModelBuilder.Menu+"6. Позы для проверки",false,355)]
    static void Open(){GetWindow<LeoPosePreview>("Лео — позы");}
    void OnGUI()
    {
        EditorGUILayout.HelpBox("Проверочные позы рига. Выбери модель Лео; движения не выдают ресурсы и не запускают стройку. Перед Play верни Rest.",MessageType.Info);
        rig=(LeoRig)EditorGUILayout.ObjectField("Риг",rig,typeof(LeoRig),true);
        if(GUILayout.Button("Взять выделенную модель")&&Selection.activeGameObject!=null)rig=Selection.activeGameObject.GetComponentInParent<LeoRig>();
        pose=(Pose)EditorGUILayout.EnumPopup("Поза",pose);
        using(new EditorGUI.DisabledScope(rig==null||EditorUtility.IsPersistent(rig)||EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if(GUILayout.Button("Показать позу")){Undo.RegisterFullObjectHierarchyUndo(rig.gameObject,"Leo rig preview");Apply(rig,pose);EditorUtility.SetDirty(rig);}
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Новая переноска — заполнение",EditorStyles.boldLabel);
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("0/21"))PreviewCargo(0,21);
                if(GUILayout.Button("4/21"))PreviewCargo(4,21);
                if(GUILayout.Button("12/21"))PreviewCargo(12,21);
                if(GUILayout.Button("21/21"))PreviewCargo(21,21);
                if(GUILayout.Button("42/42"))PreviewCargo(42,42);
                if(GUILayout.Button("64/64"))PreviewCargo(64,64);
            }
            if(GUILayout.Button("Вернуть Rest")){Undo.RegisterFullObjectHierarchyUndo(rig.gameObject,"Leo rest preview");Apply(rig,Pose.Rest);EditorUtility.SetDirty(rig);}
        }
    }
    void PreviewCargo(int carried,int capacity)
    {
        Undo.RegisterFullObjectHierarchyUndo(rig.gameObject,"Leo carrier "+carried+"/"+capacity);
        Apply(rig,Pose.CarryFull);rig.ShowCargo(carried,capacity);
        EditorUtility.SetDirty(rig);
    }
    public static void Apply(LeoRig rig,Pose pose)
    {
        if(rig==null)return;if(!rig.HasRest)rig.CaptureRest();rig.ResetPose();rig.ShowCargo(0,1);rig.EquipHammer(false);
        switch(pose)
        {
            case Pose.TPose:
                rig.Rotate(rig.upperArmR,new Vector3(0,0,81));rig.Rotate(rig.upperArmL,new Vector3(0,0,-81));
                if(rig.axeRoot!=null)rig.axeRoot.gameObject.SetActive(false);break;
            case Pose.AxeReady:
                rig.Rotate(rig.upperArmR,new Vector3(-75,0,-8));rig.Rotate(rig.elbowR,new Vector3(-55,0,0));
                rig.Rotate(rig.upperArmL,new Vector3(-30,0,-12));rig.Rotate(rig.chest,new Vector3(0,-8,0));
                rig.handR.rotation=rig.transform.rotation*Quaternion.Euler(-12,0,-8);break;
            case Pose.AxeContact:
                rig.Rotate(rig.upperArmR,new Vector3(-58,0,-5));rig.Rotate(rig.elbowR,new Vector3(-18,0,0));
                rig.Rotate(rig.upperArmL,new Vector3(-24,0,-10));rig.Rotate(rig.chest,new Vector3(7,8,0));
                rig.handR.rotation=rig.transform.rotation*Quaternion.Euler(68,0,-8);break;
            case Pose.CarryFull:
                rig.ShowCargo(21,21);rig.Rotate(rig.spine,new Vector3(5,0,0));rig.Rotate(rig.upperArmL,new Vector3(-10,0,-6));break;
            case Pose.BuildReady:
                rig.EquipHammer(true);rig.Rotate(rig.upperArmR,new Vector3(-58,0,-12));rig.Rotate(rig.elbowR,new Vector3(-40,0,0));
                rig.Rotate(rig.upperArmL,new Vector3(-35,0,-12));rig.Rotate(rig.head,new Vector3(12,0,0));
                rig.handR.rotation=rig.transform.rotation*Quaternion.Euler(10,0,-5);break;
            case Pose.Talk:
                rig.Rotate(rig.upperArmL,new Vector3(-28,0,-16));rig.Rotate(rig.elbowL,new Vector3(-38,0,0));
                rig.Rotate(rig.browL,new Vector3(0,0,-6));rig.Rotate(rig.head,new Vector3(-2,8,-2));break;
            case Pose.Compass:
                rig.Rotate(rig.upperArmL,new Vector3(-50,0,-18));rig.Rotate(rig.elbowL,new Vector3(-70,0,0));
                rig.Rotate(rig.forearmTwistL,new Vector3(0,-35,0));rig.Rotate(rig.head,new Vector3(14,-12,0));break;
        }
        rig.PlaceAxe(pose==Pose.Talk||pose==Pose.Compass?1:0);
        rig.PlaceHammer();
    }
}
