using System;
using UnityEngine;

/// <summary>ゲーム画面から外の画面担当へ渡す操作要求。戦闘の出来事は従来のGameEventsを使う。</summary>
public static class GameScreenEvents
{
    public static event Action TitleRequested;
    public static bool HasTitleHandler=>TitleRequested!=null;
    public static void RaiseTitleRequested()
    {
        if(TitleRequested==null)return;
        foreach(Action handler in TitleRequested.GetInvocationList())
        {
            try{handler();}catch(Exception error){Debug.LogException(error);}
        }
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()=>TitleRequested=null;
}
