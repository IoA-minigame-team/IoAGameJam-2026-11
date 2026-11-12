using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>共有イベントを受ける確認用の和風UI。HP・命中・撃破を代わりに発行しない。</summary>
[DefaultExecutionOrder(-300)]
public sealed class InkReviewInterface : MonoBehaviour
{
    [SerializeField] Font japaneseFont;
    [SerializeField] float bannerDuration=2.2f;
    [SerializeField] Canvas canvas;
    [SerializeField] Text waveText,remainingText,queuedText,noticeText,instructions;
    [SerializeField] Image remainingBar;
    [SerializeField] CanvasGroup bannerGroup;
    [SerializeField] CanvasGroup gameOverGroup,titleButtonGroup;
    [SerializeField] Text gameOverSubtitle,titleHint;
    [SerializeField] Button replayButton,retryWaveButton,titleButton;
    [SerializeField] Text bannerTitle,bannerSub,bannerSeal;
    [SerializeField] Button pauseButton,resetButton,guardButton,damageButton;
    [SerializeField] Text pauseCaption,damageCaption;
    [SerializeField] GameObject reviewControls,reviewCards;
    [SerializeField] Text[] cardStatus=new Text[3];
    InkReviewSession session;
    readonly object hoverPauseOwner=new object();
    readonly Dictionary<Enemy,Actor> actors=new Dictionary<Enemy,Actor>();
    readonly int[] attackCounts=new int[3];
    float bannerRemaining,noticeRemaining;
    int currentWave,totalThisWave;
    bool hoverPaused,menuPaused;
    bool bannerBlocksSpawning,manualPaused,finished;
    int inputReleaseFrames;
    int pointerReleaseFrames;
    Button[] gameplayButtons;
    public bool IsBannerVisible=>bannerRemaining>0;
    public bool IsPaused=>manualPaused;
    public bool IsFinished=>finished;
    public bool IsGameOverVisible=>gameOverGroup.alpha>0;
    public Button ReplayButton=>replayButton;
    public Button RetryWaveButton=>retryWaveButton;
    public Button TitleButton=>titleButton;
    public string RemainingLabel=>remainingText.text;
    public string BannerLabel=>bannerTitle.text;
    public Button PauseButton=>pauseButton;
    public Button DamageButton=>damageButton;
    public Button GuardButton=>guardButton;
    public Button ResetButton=>resetButton;
    public Canvas Canvas=>canvas;
    sealed class Actor
    {
        public Enemy enemy;public EnemyPursuit pursuit;public EnemyAutoAttack attack;public EnemyActionAnimation animation;
        public EnemyBehaviourRole role;public float guardAt;public bool guardSet;
    }
    static readonly Color INK=new Color(.055f,.055f,.055f),PAPER=new Color(.95f,.95f,.95f),ASH=new Color(.6f,.6f,.6f);
    void Awake()
    {
        session=GetComponent<InkReviewSession>();canvas.worldCamera=Camera.main;
        reviewControls.SetActive(session.IsReview);reviewCards.SetActive(session.IsReview);
        pauseButton.onClick.AddListener(TogglePause);resetButton.onClick.AddListener(session.ResetLayout);guardButton.onClick.AddListener(session.GuardLayout);damageButton.onClick.AddListener(session.ToggleDamage);
        replayButton.onClick.AddListener(session.ReplayAll);retryWaveButton.onClick.AddListener(session.RetryWave);titleButton.onClick.AddListener(session.RequestTitle);
        gameplayButtons=new[]{pauseButton,resetButton,guardButton,damageButton};
        SetGroup(bannerGroup,false);
        SetGroup(gameOverGroup,false);
        instructions.text=session.IsReview?"WASD 移動　／　左クリックを押し続けて刀を置く → 移動 → 離して斬り戻す":"WASD 移動　／　左クリックを押して刀を置く　／　離して斬り戻す";
    }
    void OnEnable()
    {
        GameEvents.WaveStarted+=OnWaveStarted;GameEvents.WaveCleared+=OnWaveCleared;GameEvents.GameOver+=OnGameOver;
        GameEvents.SwordPlanted+=OnSword;GameEvents.SlashStarted+=OnSlash;GameEvents.PlayerDamaged+=OnDamaged;
        GameEvents.EnemyFell+=OnFell;EnemyMotionEvents.AttackStarted+=OnAttack;
        // 無効中は停止要求を解放するが、「一時停止中」「開戦表示中」という画面状態は維持する。
        UpdatePause();
    }
    void OnDisable()
    {
        GameEvents.WaveStarted-=OnWaveStarted;GameEvents.WaveCleared-=OnWaveCleared;GameEvents.GameOver-=OnGameOver;
        GameEvents.SwordPlanted-=OnSword;GameEvents.SlashStarted-=OnSlash;GameEvents.PlayerDamaged-=OnDamaged;
        GameEvents.EnemyFell-=OnFell;EnemyMotionEvents.AttackStarted-=OnAttack;
        TimeController.ReleasePause(this);TimeController.ReleasePause(hoverPauseOwner);
        // これはTimeControllerへ適用済みかのキャッシュ。再表示時に停止要求を出し直せるようにする。
        menuPaused=false;hoverPaused=false;
    }
    void OnWaveStarted(int number)
    {
        currentWave=number;totalThisWave=0;
        foreach(var batch in session.Flow.Plan.Waves[number-1].Batches)totalThisWave+=batch.Count;
        bannerTitle.text="第"+Number(number)+"波";
        bannerSub.text=session.IsReview?"影　・　盾持ち　・　灯喰らい　　同時出現":"影を誘い、一振りで払え";
        bannerSeal.text="開　戦";bannerBlocksSpawning=true;bannerRemaining=bannerDuration;SetGroup(bannerGroup,true);UpdatePause();
        waveText.text="第"+Number(number)+"波　／　全"+Number(session.Flow.TotalWaves)+"波";
        remainingText.text="残り  "+totalThisWave.ToString("D2")+"  ／  "+totalThisWave.ToString("D2");
        queuedText.text="出現待ち　"+session.Flow.PendingCount+" 体";remainingBar.fillAmount=1;remainingBar.rectTransform.localScale=Vector3.one;
    }
    void OnWaveCleared(int number)
    {
        Notice("第"+Number(number)+"波　突破");
        if(session.Flow.State==WaveController.WaveState.Completed)ShowEndBand("稽古終了","すべての影を場外へ払った");
    }
    void OnGameOver()
    {
        finished=true;manualPaused=false;inputReleaseFrames=0;bannerBlocksSpawning=false;bannerRemaining=0;
        reviewControls.SetActive(false);reviewCards.SetActive(false);instructions.gameObject.SetActive(false);pauseButton.gameObject.SetActive(false);
        SetGroup(bannerGroup,false);gameOverSubtitle.text="GAME OVER　／　第"+Number(Mathf.Max(1,currentWave))+"波";
        SetGroup(gameOverGroup,true);UpdateTitleAvailability();UpdatePause();
        if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(replayButton.gameObject);
    }
    void UpdateTitleAvailability()
    {
        bool ready=GameScreenEvents.HasTitleHandler;titleButton.interactable=ready;titleButtonGroup.alpha=ready?1:.4f;
        titleHint.text=ready?"":"タイトル画面は準備中";
    }
    void ShowEndBand(string heading,string sub)
    {
        finished=true;manualPaused=false;inputReleaseFrames=0;bannerBlocksSpawning=false;
        bannerTitle.text=heading;bannerSub.text=sub;bannerSeal.text="終　了";
        bannerRemaining=3;SetGroup(bannerGroup,true);bannerGroup.interactable=false;bannerGroup.blocksRaycasts=false;UpdatePause();
    }
    void OnSword(Vector3 position)=>Notice("刀を設置　―　灯喰らいは刀へ向かう");
    void OnSlash(SlashStartedInfo info)=>Notice("斬り戻し　―　灯喰らいは主人公へ戻る");
    void OnDamaged(int remaining)=>Notice(remaining>0?"接触被弾　―　残り "+remaining+" 回":"接触被弾　―　力尽きた");
    void OnFell(EnemyFellInfo info)=>Notice("影を場外へ払った");
    void OnAttack(ILaunchable source)
    {
        foreach(var actor in actors.Values)if(ReferenceEquals(actor.enemy,source)){attackCounts[Index(actor.role)]++;break;}
    }
    void Notice(string text){noticeText.text=text;noticeRemaining=3f;}
    void Update()
    {
        if(session==null)return;
        float dt=Time.unscaledDeltaTime;
        if(IsGameOverVisible)UpdateTitleAvailability();
        if(inputReleaseFrames>0){inputReleaseFrames--;UpdatePause();}
        if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)TogglePause();
        if(bannerRemaining>0)
        {
            bannerRemaining=Mathf.Max(0,bannerRemaining-dt);
            bannerGroup.alpha=Mathf.Min(1,bannerRemaining/.3f);
            if(bannerRemaining<=0){SetGroup(bannerGroup,false);bannerBlocksSpawning=false;UpdatePause();}
        }
        if(noticeRemaining>0){noticeRemaining-=dt;noticeText.color=new Color(1,1,1,Mathf.Clamp01(noticeRemaining));}
        int remaining=session.Flow.PendingCount+session.Flow.Spawner.AliveCount;
        remainingText.text="残り  "+remaining.ToString("D2")+"  ／  "+totalThisWave.ToString("D2");
        queuedText.text=session.Flow.PendingCount>0?"出現待ち　"+session.Flow.PendingCount+" 体":"島上の影　"+session.Flow.Spawner.AliveCount+" 体";
        remainingBar.fillAmount=totalThisWave>0?Mathf.Clamp01(remaining/(float)totalThisWave):0;
        remainingBar.rectTransform.localScale=new Vector3(remainingBar.fillAmount,1,1);
        if(session.IsReview){RefreshActors();damageCaption.text=session.DamageEnabled?"接触被弾：有効":"接触被弾：停止";}
        UpdateHoverPause();
    }
    void UpdateHoverPause()
    {
        bool overButton=false;
        if(!finished&&Mouse.current!=null)
        {
            var point=Mouse.current.position.ReadValue();
            foreach(var button in gameplayButtons)
                if(button.gameObject.activeInHierarchy&&RectTransformUtility.RectangleContainsScreenPoint((RectTransform)button.transform,point,canvas.worldCamera)){overButton=true;break;}
        }
        // ホバーだけでゲームを止めず、UIを押すフレームだけ主人公の生入力と分ける。
        if(overButton&&Mouse.current.leftButton.isPressed)pointerReleaseFrames=2;
        else if(pointerReleaseFrames>0)pointerReleaseFrames--;
        bool hovering=pointerReleaseFrames>0;
        if(hovering==hoverPaused)return;hoverPaused=hovering;
        if(hovering)TimeController.RequestPause(hoverPauseOwner);else TimeController.ReleasePause(hoverPauseOwner);
    }
    void RefreshActors()
    {
        foreach(var enemy in session.Flow.Spawner.OwnedEnemies)
        {
            if(enemy==null||actors.ContainsKey(enemy))continue;
            var actor=new Actor{enemy=enemy,pursuit=enemy.GetComponent<EnemyPursuit>(),attack=enemy.GetComponent<EnemyAutoAttack>(),animation=enemy.GetComponent<EnemyActionAnimation>(),role=enemy.Tuning.BehaviourRole,guardAt=Time.time+enemy.Tuning.SpawnHoldDuration+.05f};
            actors.Add(enemy,actor);AddMarker(actor);
            if(session.GuardPractice&&actor.role!=EnemyBehaviourRole.LampEater)
            {
                actor.pursuit.enabled=false;actor.attack.enabled=false;
                if(actor.role==EnemyBehaviourRole.ShieldBearer)enemy.transform.rotation=Quaternion.LookRotation(Vector3.left);
            }
        }
        string[] status={"出現待ち","出現待ち","出現待ち"};
        foreach(var actor in actors.Values)
        {
            int index=Index(actor.role);
            if(actor.enemy==null){if(status[index]=="出現待ち")status[index]="場外落下";continue;}
            if(session.GuardPractice&&actor.role==EnemyBehaviourRole.ShieldBearer&&!actor.guardSet&&Time.time>=actor.guardAt){actor.guardSet=true;EnemyMotionEvents.RaiseGuardChanged(actor.enemy,true);}
            string target=actor.pursuit.Target==EnemyPursuit.PursuitTarget.Sword?"刀":actor.pursuit.Target==EnemyPursuit.PursuitTarget.Player?"主人公":"固定配置";
            status[index]="追跡先　"+target+"\n自動攻撃　"+attackCounts[index]+" 回";
            if(actor.role==EnemyBehaviourRole.ShieldBearer)status[index]+="　／　"+(actor.animation.IsDefending?"盾を構え中":"姿勢移行中");
            if(actor.enemy.State!=Enemy.MotionState.Idle)status[index]+="\n"+(actor.enemy.State==Enemy.MotionState.Flying?"吹き飛び中":"縁・落下の反応");
        }
        for(int i=0;i<3;i++)cardStatus[i].text=status[i];
        if(session.GuardPractice&&noticeRemaining<=0){noticeText.text="正面ガード稽古：刀を置き、W → D → S で敵を迂回して、右側で離す";noticeText.color=Color.white;}
    }
    void AddMarker(Actor actor)
    {
        var root=new GameObject("ReviewNameMarker",typeof(RectTransform),typeof(Canvas));root.transform.SetParent(actor.enemy.transform,false);
        root.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;root.transform.localScale=Vector3.one*.01f;
        var rect=(RectTransform)root.transform;rect.sizeDelta=new Vector2(340,60);
        var image=Panel(root.transform,"Backing",new Vector2(.5f,.5f),Vector2.zero,new Vector2(340,60),new Color(.04f,.04f,.04f,.85f));
        Label(image.transform,"Name",Name(actor.role),34,Color.white,Vector2.zero,new Vector2(330,56));
        root.AddComponent<InkReviewMarker>();
    }
    public void TogglePause()
    {
        if(finished)return;
        manualPaused=!manualPaused;
        if(!manualPaused)inputReleaseFrames=2;
        pauseCaption.text=manualPaused?"再開":"一時停止";
        Notice(manualPaused?"一時停止　―　ボタンまたはEscで再開":"再開");UpdatePause();
    }
    void UpdatePause()
    {
        bool pause=manualPaused||(bannerBlocksSpawning&&bannerRemaining>0)||inputReleaseFrames>0;
        if(pause==menuPaused)return;menuPaused=pause;
        if(pause)TimeController.RequestPause(this);else TimeController.ReleasePause(this);
    }
    static int Index(EnemyBehaviourRole role)=>role==EnemyBehaviourRole.Shadow?0:role==EnemyBehaviourRole.ShieldBearer?1:2;
    static string Name(EnemyBehaviourRole role)=>role==EnemyBehaviourRole.Shadow?"影":role==EnemyBehaviourRole.ShieldBearer?"盾持ち":"灯喰らい";
    static string Number(int number)=>number>=1&&number<=5?new[]{"一","二","三","四","五"}[number-1]:number.ToString();
    static void SetGroup(CanvasGroup group,bool visible){group.alpha=visible?1:0;group.interactable=group.blocksRaycasts=visible;}

    /// <summary>制作時に呼び、編集できるuGUIの部品としてPrefabへ保存する。</summary>
    public void Construct(Font font)
    {
        japaneseFont=font;
        var canvasRoot=new GameObject("InkCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvasRoot.transform.SetParent(transform,false);
        canvas=canvasRoot.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.planeDistance=1;canvas.sortingOrder=100;
        var scaler=canvasRoot.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
        var hud=Panel(canvas.transform,"WavePlate",new Vector2(.5f,1),new Vector2(0,-68),new Vector2(820,102),PAPER);
        Panel(hud.transform,"InkEdge",new Vector2(0,.5f),new Vector2(4,0),new Vector2(8,102),INK);
        waveText=Label(hud.transform,"Wave","第 一 波",30,INK,new Vector2(-205,18),new Vector2(350,46),TextAnchor.MiddleLeft);
        remainingText=Label(hud.transform,"Remaining","残り  00 ／ 00",37,INK,new Vector2(210,14),new Vector2(350,48),TextAnchor.MiddleRight);
        queuedText=Label(hud.transform,"Queued","開戦を待つ",20,new Color(.3f,.3f,.3f),new Vector2(-200,-22),new Vector2(360,30),TextAnchor.MiddleLeft);
        var track=Panel(hud.transform,"ProgressTrack",new Vector2(.5f,.5f),new Vector2(210,-23),new Vector2(330,4),new Color(.75f,.75f,.75f));
        remainingBar=Panel(track.transform,"Progress",new Vector2(.5f,.5f),Vector2.zero,new Vector2(330,4),INK);remainingBar.type=Image.Type.Filled;remainingBar.fillMethod=Image.FillMethod.Horizontal;
        remainingBar.rectTransform.pivot=new Vector2(0,.5f);remainingBar.rectTransform.anchoredPosition=new Vector2(-165,0);
        Label(canvas.transform,"Brand","ヒトフリ",29,PAPER,new Vector2(0,0),new Vector2(200,55)).rectTransform.SetPosition(new Vector2(0,1),new Vector2(120,-53));
        noticeText=Label(canvas.transform,"Notice","",23,Color.white,Vector2.zero,new Vector2(1350,60));noticeText.rectTransform.SetPosition(new Vector2(.5f,1),new Vector2(0,-150));
        pauseButton=ButtonAt(canvas.transform,"PauseButton","一時停止",new Vector2(1,1),new Vector2(-100,-53),new Vector2(140,48),out pauseCaption);
        reviewControls=new GameObject("ReviewControls",typeof(RectTransform));reviewControls.transform.SetParent(canvas.transform,false);Stretch((RectTransform)reviewControls.transform);
        resetButton=ButtonAt(reviewControls.transform,"ResetLayoutButton","配置を戻す",new Vector2(.5f,0),new Vector2(-290,244),new Vector2(250,42),out _);
        guardButton=ButtonAt(reviewControls.transform,"GuardLayoutButton","正面ガード配置",new Vector2(.5f,0),new Vector2(0,244),new Vector2(250,42),out _);
        damageButton=ButtonAt(reviewControls.transform,"DamageButton","接触被弾：停止",new Vector2(.5f,0),new Vector2(290,244),new Vector2(250,42),out damageCaption);
        reviewCards=new GameObject("EnemyCards",typeof(RectTransform));reviewCards.transform.SetParent(canvas.transform,false);Stretch((RectTransform)reviewCards.transform);
        string[] titles={"影","盾持ち","灯喰らい"},hints={"主人公を追いかける","正面は防ぎ、背面は飛ぶ","置かれた刀へ群がる"};
        for(int i=0;i<3;i++)
        {
            var card=Panel(reviewCards.transform,"EnemyCard"+i,new Vector2(.5f,0),new Vector2((i-1)*440,134),new Vector2(410,150),PAPER);
            Panel(card.transform,"InkTop",new Vector2(.5f,1),new Vector2(0,-3),new Vector2(410,6),INK);
            Label(card.transform,"Ordinal","0"+(i+1),21,new Color(.35f,.35f,.35f),new Vector2(-165,42),new Vector2(55,34));
            Label(card.transform,"Role",titles[i],34,INK,new Vector2(-25,42),new Vector2(250,42));
            Label(card.transform,"Hint",hints[i],20,new Color(.3f,.3f,.3f),new Vector2(0,4),new Vector2(390,30));
            cardStatus[i]=Label(card.transform,"Status","出現待ち",20,INK,new Vector2(0,-38),new Vector2(390,65));
        }
        instructions=Label(canvas.transform,"Instructions","",22,PAPER,Vector2.zero,new Vector2(1500,42));instructions.rectTransform.SetPosition(new Vector2(.5f,0),new Vector2(0,28));
        bannerGroup=Overlay(canvas.transform,"WaveAnnouncement",new Color(.015f,.015f,.015f,.72f));
        var strip=Panel(bannerGroup.transform,"PaperBand",new Vector2(.5f,.5f),Vector2.zero,new Vector2(1160,240),PAPER);
        var ring=new GameObject("Enso",typeof(RectTransform),typeof(CanvasRenderer),typeof(InkRingGraphic));ring.transform.SetParent(strip.transform,false);Place((RectTransform)ring.transform,new Vector2(.5f,.5f),new Vector2(-420,0),new Vector2(230,230));ring.GetComponent<InkRingGraphic>().color=INK;ring.GetComponent<InkRingGraphic>().raycastTarget=false;
        bannerSeal=Label(strip.transform,"Opening","開　戦",25,INK,new Vector2(-420,0),new Vector2(200,60));
        bannerTitle=Label(strip.transform,"WaveTitle","第一波",91,INK,new Vector2(80,25),new Vector2(780,110));
        bannerSub=Label(strip.transform,"WaveSubtitle","影を誘い、一振りで払え",25,INK,new Vector2(80,-66),new Vector2(900,48));
        gameOverGroup=Overlay(canvas.transform,"GameOverControls",new Color(.015f,.015f,.015f,.94f));
        var backdropRing=new GameObject("EndingEnso",typeof(RectTransform),typeof(CanvasRenderer),typeof(InkRingGraphic));backdropRing.transform.SetParent(gameOverGroup.transform,false);Place((RectTransform)backdropRing.transform,new Vector2(.5f,.5f),new Vector2(-390,0),new Vector2(620,620));backdropRing.GetComponent<InkRingGraphic>().color=new Color(.7f,.7f,.7f,.22f);backdropRing.GetComponent<InkRingGraphic>().raycastTarget=false;
        Label(gameOverGroup.transform,"Motto","影\nを\n払\nう",52,PAPER,new Vector2(-390,0),new Vector2(150,360));
        Label(gameOverGroup.transform,"GameOverHeading","終　幕",83,PAPER,new Vector2(210,225),new Vector2(750,130));
        gameOverSubtitle=Label(gameOverGroup.transform,"GameOverSubtitle","GAME OVER",25,PAPER,new Vector2(210,125),new Vector2(800,55));
        replayButton=ButtonAt(gameOverGroup.transform,"ReplayButton","最初から再挑戦",new Vector2(.5f,.5f),new Vector2(210,30),new Vector2(520,65),out _);
        retryWaveButton=ButtonAt(gameOverGroup.transform,"RetryWaveButton","このウェーブから再挑戦",new Vector2(.5f,.5f),new Vector2(210,-58),new Vector2(520,65),out _);
        titleButton=ButtonAt(gameOverGroup.transform,"TitleButton","タイトルへ",new Vector2(.5f,.5f),new Vector2(210,-146),new Vector2(520,65),out _);titleButtonGroup=titleButton.gameObject.AddComponent<CanvasGroup>();
        titleHint=Label(gameOverGroup.transform,"TitleAvailability","タイトル画面は準備中",19,ASH,new Vector2(210,-210),new Vector2(650,36));
        SetGroup(bannerGroup,false);SetGroup(gameOverGroup,false);
    }
    Image Panel(Transform parent,string name,Vector2 anchor,Vector2 position,Vector2 size,Color color)
    {
        var root=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));root.transform.SetParent(parent,false);Place((RectTransform)root.transform,anchor,position,size);var image=root.GetComponent<Image>();image.color=color;image.raycastTarget=false;return image;
    }
    Text Label(Transform parent,string name,string text,int size,Color color,Vector2 position,Vector2 dimensions,TextAnchor alignment=TextAnchor.MiddleCenter)
    {
        var root=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));root.transform.SetParent(parent,false);Place((RectTransform)root.transform,new Vector2(.5f,.5f),position,dimensions);
        var label=root.GetComponent<Text>();label.font=japaneseFont;label.fontSize=size;label.text=text;label.color=color;label.alignment=alignment;label.raycastTarget=false;label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Overflow;return label;
    }
    Button ButtonAt(Transform parent,string name,string text,Vector2 anchor,Vector2 position,Vector2 size,out Text caption)
    {
        var image=Panel(parent,name,anchor,position,size,PAPER);image.raycastTarget=true;var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;button.transition=Selectable.Transition.None;
        caption=Label(image.transform,"Caption",text,25,INK,Vector2.zero,size-new Vector2(16,4));image.gameObject.AddComponent<InkButtonFeedback>().Configure(image,caption);return button;
    }
    CanvasGroup Overlay(Transform parent,string name,Color color)
    {
        var image=Panel(parent,name,new Vector2(.5f,.5f),Vector2.zero,Vector2.zero,color);Stretch((RectTransform)image.transform);image.raycastTarget=true;return image.gameObject.AddComponent<CanvasGroup>();
    }
    static void Place(RectTransform rect,Vector2 anchor,Vector2 position,Vector2 size){rect.anchorMin=rect.anchorMax=anchor;rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;}
    static void Stretch(RectTransform rect){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
}

public static class InkRectExtensions
{
    public static void SetPosition(this RectTransform rect,Vector2 anchor,Vector2 position){rect.anchorMin=rect.anchorMax=anchor;rect.anchoredPosition=position;}
}
