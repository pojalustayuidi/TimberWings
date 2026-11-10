using UnityEngine;

public sealed class WorkyardStockDisplay : MonoBehaviour
{
    public GameObject[] logUnits=new GameObject[0],plankUnits=new GameObject[0];
    public GameObject logsFull,planksFull;
    public void Refresh(WorkerManager manager)
    {
        bool valid=manager!=null && manager.Sim!=null;
        Show(logUnits,valid?WorkyardReadout.VisibleUnits(manager.Logs,manager.LogCap,logUnits.Length):0);
        Show(plankUnits,valid?WorkyardReadout.VisibleUnits(manager.Planks,manager.PlankCap,plankUnits.Length):0);
        if(logsFull!=null)logsFull.SetActive(valid && manager.Logs>=manager.LogCap);
        if(planksFull!=null)planksFull.SetActive(valid && manager.Planks>=manager.PlankCap);
    }
    static void Show(GameObject[] units,int count)
    {
        for(int i=0;i<units.Length;i++)if(units[i]!=null && units[i].activeSelf!=(i<count))units[i].SetActive(i<count);
    }
}
