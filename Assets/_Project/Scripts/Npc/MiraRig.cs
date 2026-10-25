// Assets/_Project/Scripts/Npc/MiraRig.cs   (НЕ в папке Editor!)
// Ссылки на суставы модели Миры. Модель собирает генератор (Tools > BuilderSim > NPC > Мира: модель), ссылки заполняет он же.
// Сам ничего не анимирует: это «розетка» под будущие анимации (код, как у HutSawyer, или Animator).
//
//   • суставы: hips, spine, head, eyeR/eyeL (моргание: scale.y), shoulder/elbow/hand, hip/knee/ankle
//   • реквизит: mug (кружка, в правой руке), charm (шишка-брелок), gloves, hatchet, rope: у каждого свой сустав-маятник
//   • CaptureRest() запоминает позу покоя (её записывает генератор), ResetPose() возвращает в неё после анимации
// Правая/левая: R = правая рука Миры (на референсе слева), лицо смотрит в +Z.
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("BuilderSim/Mira Rig")]
public class MiraRig : MonoBehaviour
{
    [Header("Корпус")]
    public Transform hips;
    public Transform spine;
    public Transform head;

    [Header("Лицо")]
    public Transform eyeR;
    public Transform eyeL;

    [Header("Руки (R держит кружку, L на поясе)")]
    public Transform shoulderR, elbowR, handR;
    public Transform shoulderL, elbowL, handL;

    [Header("Ноги")]
    public Transform hipR, kneeR, ankleR;
    public Transform hipL, kneeL, ankleL;

    [Header("Реквизит")]
    public Transform mug;
    public Transform charm;
    public Transform gloves;
    public Transform hatchet;
    public Transform rope;

    [SerializeField, HideInInspector] Transform[] restBones;
    [SerializeField, HideInInspector] Vector3[] restPos;
    [SerializeField, HideInInspector] Quaternion[] restRot;
    [SerializeField, HideInInspector] Vector3[] restScale;

    // имя узла в иерархии модели → поле
    public void Bind()
    {
        var map = new Dictionary<string, Transform>();
        foreach (var t in GetComponentsInChildren<Transform>(true)) map[t.name] = t;
        Transform T(string n) { Transform r; return map.TryGetValue(n, out r) ? r : null; }

        hips = T("Hips"); spine = T("Spine"); head = T("Head");
        eyeR = T("EyeR"); eyeL = T("EyeL");
        shoulderR = T("ShoulderR"); elbowR = T("ElbowR"); handR = T("HandR");
        shoulderL = T("ShoulderL"); elbowL = T("ElbowL"); handL = T("HandL");
        hipR = T("HipR"); kneeR = T("KneeR"); ankleR = T("AnkleR");
        hipL = T("HipL"); kneeL = T("KneeL"); ankleL = T("AnkleL");
        mug = T("Mug"); charm = T("Charm"); gloves = T("Gloves"); hatchet = T("Hatchet"); rope = T("Rope");
    }

    // запомнить текущую позу как позу покоя (суставы = всё, что не меш)
    public void CaptureRest()
    {
        var list = new List<Transform>();
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t != transform && t.GetComponent<MeshFilter>() == null) list.Add(t);
        restBones = list.ToArray();
        restPos = new Vector3[restBones.Length];
        restRot = new Quaternion[restBones.Length];
        restScale = new Vector3[restBones.Length];
        for (int i = 0; i < restBones.Length; i++)
        {
            restPos[i] = restBones[i].localPosition;
            restRot[i] = restBones[i].localRotation;
            restScale[i] = restBones[i].localScale;
        }
    }

    // вернуть все суставы в позу покоя
    public void ResetPose()
    {
        if (restBones == null) return;
        for (int i = 0; i < restBones.Length; i++)
        {
            if (restBones[i] == null) continue;
            restBones[i].localPosition = restPos[i];
            restBones[i].localRotation = restRot[i];
            restBones[i].localScale = restScale[i];
        }
    }
}
