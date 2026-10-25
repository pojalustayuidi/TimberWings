// Assets/_Project/Scripts/UI/Candy/CandyFxKit.cs (НЕ в Editor)
// Набор спрайтов для эффектов. Создаётся меню Tools > BuilderSim > UI > Candy Kit > Fun > 1. Собрать CandyFxKit
// и подставляется во все скрипты Candy-эффектов автоматически.
using UnityEngine;

[CreateAssetMenu(fileName = "CandyFxKit", menuName = "BuilderSim/Candy Fx Kit")]
public class CandyFxKit : ScriptableObject
{
    public Sprite sparkle;
    public Sprite sparkleSmall;
    public Sprite glow;
    public Sprite ring;
    public Sprite heart;
    public Sprite confettiRect;
    public Sprite confettiCircle;
    public Sprite confettiTri;
}
